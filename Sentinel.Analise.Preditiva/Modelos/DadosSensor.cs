namespace Sentinel.Analise.Preditiva.Modelos
{
    public class DadosSensor
    {
        public long Id { get; set; }
        public string IdMaquina { get; set; }
        public string IdSensor { get; set; }
        public TipoSensor TipoSensor { get; set; }
        public double Valor { get; set; }
        public DateTime DataHora { get; set; }
        public bool Enviado { get; set; }
    }

    public enum TipoSensor
    {
        Temperatura,
        FluidoArrefecimento,
        RotacaoMotor
    }
}
