using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sentinel.Analise.Preditiva.Modelos;
using Sentinel.Analise.Preditiva.Utils;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Sentinel.Analise.Preditiva.Services
{
    public class RabbitMQService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ConcurrentDictionary<(string IdMaquina, DateTime DataHora), List<DadosSensor>> _buffer = new();
        private readonly ElasticsearchService _elasticsearchService;

        public RabbitMQService(IConnection connection, IConfiguration configuration, ElasticsearchService elasticsearchService)
        {
            _configuration = configuration;
            _connection = connection;
            _channel = _connection.CreateModel();
            _elasticsearchService = elasticsearchService;
            ConfigurarFilas();
        }

        private void ConfigurarFilas()
        {
            ConfigurarFila("DadosSensor");
            ConfigurarFila("Alerta");
            ConfigurarFila("Critico");
        }

        private void ConfigurarFila(string tipo)
        {
            var exchange = _configuration[$"RabbitMQ:Exchanges:{tipo}"] ?? $"{tipo.ToLower()}-exchange";
            var queue = _configuration[$"RabbitMQ:Queues:{tipo}"] ?? $"{tipo.ToLower()}-queue";
            var routingKey = _configuration[$"RabbitMQ:RoutingKeys:{tipo}"] ?? $"{tipo.ToLower()}-key";
            _channel.QueueBind(queue, exchange, routingKey);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += ProcessarMensagem;
            var queue = _configuration["RabbitMQ:Queues:DadosSensor"] ?? "dados-sensor-queue";
            _channel.BasicConsume(queue, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        private void ProcessarMensagem(object? model, BasicDeliverEventArgs ea)
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            if (JsonSerializer.Deserialize<DadosSensor>(message) is { } sensorData)
            {
                ArmazenarNoBuffer(sensorData);
            }
            _channel.BasicAck(ea.DeliveryTag, multiple: false);
        }

        private void ArmazenarNoBuffer(DadosSensor sensor)
        {
            var chave = (sensor.IdMaquina, sensor.DataHora);
            _buffer.AddOrUpdate(chave, new List<DadosSensor> { sensor }, (_, lista) => { lista.Add(sensor); return lista; });
            if (_buffer[chave].Count == 3 && _buffer[chave].Select(s => s.TipoSensor).Distinct().Count() == 3)
            {
                ProcessarDados(_buffer[chave]);
                _buffer.TryRemove(chave, out _);
            }
        }

        private async void ProcessarDados(List<DadosSensor> sensores)
        {
            var analisePreditiva = CriarAnalisePreditiva(sensores);
            await _elasticsearchService.IndexarDadosAsync(analisePreditiva);
            await EnviarAlertaSeNecessario(analisePreditiva);
        }

        private AnalisePreditiva CriarAnalisePreditiva(List<DadosSensor> sensores)
        {
            var temp = sensores.FirstOrDefault(s => s.TipoSensor == TipoSensor.Temperatura)?.Valor ?? 0;
            var fluido = sensores.FirstOrDefault(s => s.TipoSensor == TipoSensor.FluidoArrefecimento)?.Valor ?? 0;
            var rotacao = sensores.FirstOrDefault(s => s.TipoSensor == TipoSensor.RotacaoMotor)?.Valor ?? 0;

            var classificacao = ClassificarDados(temp, fluido, rotacao);
            return new AnalisePreditiva
            {
                Id = GeradorIdUtil.ProximoId(),
                IdMaquina = sensores.First().IdMaquina,
                Horario = sensores.First().DataHora,
                Temperatura = temp,
                FluidoArrefecimento = fluido,
                RotacaoMotor = rotacao,
                Classificacao = classificacao,
                Info = classificacao switch
                {
                    Classificacao.Critico => "Dados críticos",
                    Classificacao.Alerta => "Dados em alerta",
                    _ => "Dados normais"
                }
            };
        }

        private async Task EnviarAlertaSeNecessario(AnalisePreditiva analisePreditiva)
        {
            if (analisePreditiva.Classificacao == Classificacao.Normal) return;
            var alerta = new Alerta
            {
                Id = GeradorIdUtil.ProximoId(),
                Mensagem = GerarMensagemAlerta(analisePreditiva),
                Checado = false
            };
            await EnviarParaExchangeAsync(alerta, analisePreditiva.Classificacao == Classificacao.Critico ? "critico" : "alerta");
        }

        private string GerarMensagemAlerta(AnalisePreditiva analise)
        {
            return analise.Classificacao switch
            {
                Classificacao.Critico => $"ALERTA CRÍTICO: Máquina {analise.IdMaquina} em condições críticas! Temperatura: {analise.Temperatura}°C, Fluido: {analise.FluidoArrefecimento}%, Rotação: {analise.RotacaoMotor} RPM.",
                Classificacao.Alerta => $"ALERTA: Máquina {analise.IdMaquina} em alerta. Temperatura: {analise.Temperatura}°C, Fluido: {analise.FluidoArrefecimento}%, Rotação: {analise.RotacaoMotor} RPM.",
                _ => ""
            };
        }

        private async Task EnviarParaExchangeAsync(Alerta alerta, string tipoAlerta)
        {
            try
            {
                var exchange = _configuration[$"RabbitMQ:Exchanges:{tipoAlerta}"] ?? $"{tipoAlerta}-exchange";
                var routingKey = _configuration[$"RabbitMQ:RoutingKeys:{tipoAlerta}"] ?? $"{tipoAlerta}-key";
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(alerta));
                _channel.BasicPublish(exchange, routingKey, basicProperties: null, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar alerta para a exchange: {ex.Message}");
            }
        }

        private static Classificacao ClassificarDados(double temp, double fluido, double rotacao)
        {
            return temp > 80 || fluido < 20 || rotacao < 1000 ? Classificacao.Critico :
                   temp > 70 || fluido < 30 || rotacao < 1500 ? Classificacao.Alerta :
                   Classificacao.Normal;
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
