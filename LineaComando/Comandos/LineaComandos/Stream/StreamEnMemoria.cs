namespace PER.Comandos.LineaComandos.Stream
{
    public class StreamEnMemoria<TRead, TWrite> : IStream<TRead, TWrite>
    {
        private readonly StreamWriterEnMemoria<TWrite> _writer;

        public StreamEnMemoria(TRead datosEntrada)
        {
            Reader = new StreamReaderEnMemoria<TRead>(datosEntrada);
            _writer = new StreamWriterEnMemoria<TWrite>();
        }

        public IStreamReader<TRead> Reader { get; }
        public IStreamWriter<TWrite> Writer => _writer;

        public TWrite? ObtenerResultado() => _writer.ObtenerResultado();
    }
}
