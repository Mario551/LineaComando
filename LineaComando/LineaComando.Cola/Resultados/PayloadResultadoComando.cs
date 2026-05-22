namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public sealed class PayloadResultadoComando
    {
        public string Tipo { get; init; } = string.Empty;

        public int Version { get; init; }

        public string Formato { get; init; } = "application/json";

        public string? Contenido { get; init; }

        public string? RutaPayload { get; init; }
    }
}
