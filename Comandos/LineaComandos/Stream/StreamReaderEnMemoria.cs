using System.Runtime.CompilerServices;

namespace PER.Comandos.LineaComandos.Stream
{
    public class StreamReaderEnMemoria<T> : IStreamReader<T>
    {
        private readonly T _dato;

        public StreamReaderEnMemoria(T dato)
        {
            _dato = dato;
        }

        public Task<T> ReadAsync(CancellationToken token = default)
        {
            return Task.FromResult(_dato);
        }

        public IAsyncEnumerable<T> ReadAllAsync(CancellationToken token = default)
        {
            return ReadAllAsyncIterator(token);
        }

        private async IAsyncEnumerable<T> ReadAllAsyncIterator([EnumeratorCancellation] CancellationToken token)
        {
            yield return _dato;
        }
    }
}
