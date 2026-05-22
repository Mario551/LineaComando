namespace PER.Comandos.LineaComandos.Cola.Resultados
{
    public interface IAlmacenamientoPayloadResultadoComando
    {
        Task<PayloadResultadoComando?> GuardarAsync(
            long comandoId,
            PayloadResultadoComando payload,
            CancellationToken token = default);

        Task<string?> LeerContenidoAsync(
            PayloadResultadoComando payload,
            CancellationToken token = default);
    }
}
