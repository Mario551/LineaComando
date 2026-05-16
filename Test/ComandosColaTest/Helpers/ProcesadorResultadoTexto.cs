using PER.Comandos.LineaComandos.Cola.Resultados;

namespace ComandosColaTest.Helpers
{
    public sealed class ProcesadorResultadoTexto : IProcesadorResultadoComando
    {
        public string Tipo => "texto";

        public int Version => 1;

        public string Formato => "text/plain";

        public Task<string?> SerializarAsync(object? salida, CancellationToken token = default)
        {
            return Task.FromResult(salida?.ToString());
        }

        public Task<object?> DeserializarAsync(string? contenido, CancellationToken token = default)
        {
            return Task.FromResult<object?>(contenido);
        }
    }
}
