using System.Threading.Channels;

namespace PER.Comandos.LineaComandos.Stream
{
    public class StreamChannel<TRead, TWrite> : IStream<TRead, TWrite>
    {
        public IStreamReader<TRead> Reader { get; private set; }
        public IStreamWriter<TWrite> Writer { get; private set; }


        public StreamChannel(ChannelReader<TRead> reader, ChannelWriter<TWrite> writer)
        {
            Reader = new StreamChannelReader<TRead>(reader);
            Writer = new StreamChannelWriter<TWrite>(writer);
        }
    }

    public class StreamChannel<T> : IStream<T>
    {
        public IStreamReader<T> Reader { get; private set; }
        public IStreamWriter<T> Writer { get; private set; }


        public StreamChannel(Channel<T> canal)
        {
            Reader = new StreamChannelReader<T>(canal.Reader);
            Writer = new StreamChannelWriter<T>(canal.Writer);
        }
    }

    public class StreamChannelReader<TRead> : IStreamReader<TRead>
    {
        private ChannelReader<TRead> _reader;

        public StreamChannelReader(ChannelReader<TRead> reader)
        {
            _reader = reader;
        }

        public IAsyncEnumerable<TRead> ReadAllAsync(CancellationToken token = default)
            => _reader.ReadAllAsync(token);

        public Task<TRead> ReadAsync(CancellationToken token = default)
            => Task.Run(async () => await _reader.ReadAsync(token));
    }

    public class StreamChannelWriter<TWrite> : IStreamWriter<TWrite>
    {
        private ChannelWriter<TWrite> _writer;

        public StreamChannelWriter(ChannelWriter<TWrite> writer)
        {
            _writer = writer;
        }

        public async Task WriteAllAsync(IEnumerable<TWrite> datos, CancellationToken token = default)
        {
            foreach (TWrite dat in datos)
                await _writer.WriteAsync(dat, token);
        }

        public async Task WriteAsync(TWrite dato, CancellationToken token = default)
            => await _writer.WriteAsync(dato, token);

        public bool TryWrite(TWrite dato)
            => _writer.TryWrite(dato);
    }
}
