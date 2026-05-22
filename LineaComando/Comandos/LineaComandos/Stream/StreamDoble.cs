namespace PER.Comandos.LineaComandos.Stream
{
    public class StreamDoble<TRead, TWrite> : IStreamDoble<TRead, TWrite>
    {
        public IStream<TRead, TRead> StreamEntrada { get; private set; }
        public IStream<TWrite, TWrite> StreamSalida { get; private set; }

        public StreamDoble(IStream<TRead, TRead> streamEntrada, IStream<TWrite, TWrite> streamSalida)
        {
            StreamEntrada = streamEntrada;
            StreamSalida = streamSalida;
        }
    }
}
