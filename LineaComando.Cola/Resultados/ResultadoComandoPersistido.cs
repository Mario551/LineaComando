namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public sealed class ResultadoComandoPersistido
    {
        public long ComandoId { get; init; }

        public string Estado { get; init; } = string.Empty;

        public string? MensajeError { get; init; }

        public TimeSpan Duracion { get; init; }

        public PayloadResultadoComando? PayloadResultado { get; init; }
    }
}
