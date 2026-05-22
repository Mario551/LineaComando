namespace PER.Comandos.LineaComandos.Cola.Colas
{
    public sealed class SolicitudComando
    {
        public string RutaComando { get; init; } = string.Empty;

        public string Argumentos { get; init; } = string.Empty;

        public string? DatosDeComando { get; init; }
    }
}
