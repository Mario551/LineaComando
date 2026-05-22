namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

public interface IOrquestarMensajeEntradaAplicacion
{
    Task EjecutarAsync(CancellationToken cancellationToken);
}
