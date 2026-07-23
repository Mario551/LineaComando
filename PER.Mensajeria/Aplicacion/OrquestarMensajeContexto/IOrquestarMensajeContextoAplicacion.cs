namespace PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;

public interface IOrquestarMensajeContextoAplicacion
{
    Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken);
}
