namespace PER.Comandos.LineaComandos.Stream
{
    public interface IStream<TRead, TWrite>
    {
        IStreamReader<TRead> Reader { get; }
        IStreamWriter<TWrite> Writer { get; }
    }

    public interface IStream<T>
    {
        IStreamReader<T> Reader { get; }
        IStreamWriter<T> Writer { get; }
    }
}
