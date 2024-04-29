namespace PER.Comandos.LineaComandos.Stream
{
    public interface IStreamDoble<TRead, TWrite>
    {
        public IStream<TRead, TRead> StreamEntrada { get; }
        public IStream<TWrite, TWrite> StreamSalida { get; }
    }
}
