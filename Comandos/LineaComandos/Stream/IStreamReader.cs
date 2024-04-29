namespace PER.Comandos.LineaComandos.Stream
{
    public interface IStreamReader<T>
    {
        IAsyncEnumerable<T> ReadAllAsync(CancellationToken token = default);
        Task<T> ReadAsync(CancellationToken token = default);
    }
}
