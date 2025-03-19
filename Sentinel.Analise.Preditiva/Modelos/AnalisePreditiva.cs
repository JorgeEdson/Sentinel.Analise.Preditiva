namespace Sentinel.Analise.Preditiva.Modelos
{
    public class AnalisePreditiva
    {
        public long Id { get; set; }
        public string IdMaquina { get; set; }
        public DateTime Horario { get; set; }
        public double Temperatura { get; set; }
        public double FluidoArrefecimento { get; set; }
        public double RotacaoMotor { get; set; }
        public Classificacao Classificacao { get; set; }
        public string Info { get; set; }
    }
    public enum Classificacao
    {
        Normal,
        Alerta,
        Critico
    }
}
