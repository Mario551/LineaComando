namespace PER.Comandos.LineaComandos.Stream
{
    public class SincronizadorStreams<TRead, TWrite> : ISincronizadorStreams<TRead, TWrite>
    {
        private uint _idEjecucion;
        private CancellationTokenSource _cancelacionInterna;

        private IStreamWriter<RespuestaComando<TWrite>> _writerPrincipal;
        private Func<uint, TWrite, RespuestaComando<TWrite>> _creadorRespuesta;
        private IStream<TRead> _streamEntrada;

        public SincronizadorStreams(uint idEjecucion,
            IStream<TRead> streamEntrada,
            IStreamWriter<RespuestaComando<TWrite>> writerPrincipal,
            Func<uint, TWrite, RespuestaComando<TWrite>> creadorRespuesta)
        {
            _idEjecucion = idEjecucion;
            _writerPrincipal = writerPrincipal;
            _creadorRespuesta = creadorRespuesta;
            _streamEntrada = streamEntrada;
            _cancelacionInterna = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cancelacionInterna.Cancel();
            _cancelacionInterna.Dispose();
        }

        public IStream<TRead, TWrite> GetStreamComando()
            => new StreamSincronizador<TRead, TWrite>(_streamEntrada.Reader, EscribirEnCanalDeSalida);

        public async Task EscribirEnCanalDeEntrada(LineaComando<TRead> lineaComando, CancellationToken token = default)
            => await _streamEntrada.Writer.WriteAsync(lineaComando.Datos, token);

        public async Task EscribirEnCanalDeSalida(TWrite write, CancellationToken token = default)
        {
            try
            {
                RespuestaComando<TWrite> respuesta = _creadorRespuesta(_idEjecucion, write);
                await _writerPrincipal.WriteAsync(respuesta, _cancelacionInterna.Token);
            }
            catch 
            {
                throw;
            }
        }

        public void Finalizar()
        {
            _cancelacionInterna.Cancel();
        }
    }
}
