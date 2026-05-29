namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

public interface IOrquestarMensajeEntradaAplicacion
{
    Task EjecutarAsync(long idProcesamientoInternoMensaje, CancellationToken cancellationToken);
}
