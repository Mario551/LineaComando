namespace PER.Mensajeria.Servicio.Contexto;

public interface IContextoConversacionActivoServicio
{
    Task EjecutarAsync(
        long idConversacion,
        Func<CancellationToken, Task> accion,
        CancellationToken cancellationToken);
}
