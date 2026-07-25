namespace PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;

public interface IOrquestarMensajeContextoAplicacion
{
    Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken);

    Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        CancellationToken cancellationToken);
}
