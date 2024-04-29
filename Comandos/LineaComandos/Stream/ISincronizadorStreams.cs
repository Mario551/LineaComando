namespace PER.Comandos.LineaComandos.Stream
{
    public interface ISincronizadorStreams<TRead, TWrite> : IDisposable
    {
        Task EscribirEnCanalDeEntrada(LineaComando<TRead> lineaComando, CancellationToken token = default);

        Task EscribirEnCanalDeSalida(TWrite write, CancellationToken token = default);

        void Finalizar();
    }
}
