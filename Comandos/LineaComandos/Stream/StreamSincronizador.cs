namespace PER.Comandos.LineaComandos.Stream
{
    public class StreamSincronizador<TRead, TWrite> : IStream<TRead, TWrite>
    {
        public IStreamReader<TRead> Reader { get; private set; }
        public IStreamWriter<TWrite> Writer { get; private set; }

        public StreamSincronizador(IStreamReader<TRead> reader, Func<TWrite, CancellationToken, Task> funcWriter)
        {
            Reader = reader;
            Writer = new StreamSincronizadorWriter<TWrite>(funcWriter);
        }
    }

    public class StreamSincronizadorWriter<TWrite> : IStreamWriter<TWrite>
    {
        private Func<TWrite, CancellationToken, Task> _funcWriter;

        public StreamSincronizadorWriter(Func<TWrite, CancellationToken, Task> funcWriter)
        {
            _funcWriter = funcWriter;
        }

        public bool TryWrite(TWrite dato)
        {
            _funcWriter(dato, default).Wait();
            return true;
        }

        public async Task WriteAllAsync(IEnumerable<TWrite> datos, CancellationToken token = default)
        {
            foreach (var dato in datos)
                await _funcWriter(dato, token);
        }

        public async Task WriteAsync(TWrite dato, CancellationToken token = default)
            => await _funcWriter(dato, token);
    }
}
