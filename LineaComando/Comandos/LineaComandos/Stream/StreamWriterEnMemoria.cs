namespace PER.Comandos.LineaComandos.Stream
{
    public class StreamWriterEnMemoria<T> : IStreamWriter<T>
    {
        private T? _resultado;

        public Task WriteAsync(T dato, CancellationToken token = default)
        {
            _resultado = dato;
            return Task.CompletedTask;
        }

        public Task WriteAllAsync(IEnumerable<T> datos, CancellationToken token = default)
        {
            _resultado = datos.LastOrDefault();
            return Task.CompletedTask;
        }

        public bool TryWrite(T dato)
        {
            _resultado = dato;
            return true;
        }

        public T? ObtenerResultado() => _resultado;
    }
}
