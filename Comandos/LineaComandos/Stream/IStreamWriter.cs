namespace PER.Comandos.LineaComandos.Stream
{
    public interface IStreamWriter<T>
    {
        Task WriteAsync(T dato, CancellationToken token = default);
        Task WriteAllAsync(IEnumerable<T> datos, CancellationToken token = default);
        bool TryWrite(T dato);
    }
}
