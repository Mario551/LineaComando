namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

public interface IOrquestarMensajeEntradaAplicacion
{
    Task<ResultadoOrquestarMensajeEntrada> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken);
}
