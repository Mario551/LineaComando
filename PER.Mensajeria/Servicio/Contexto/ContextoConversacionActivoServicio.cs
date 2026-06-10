using System.Collections.Concurrent;

namespace PER.Mensajeria.Servicio.Contexto;

public class ContextoConversacionActivoServicio : IContextoConversacionActivoServicio
{
    private readonly ConcurrentDictionary<long, SemaphoreSlim> bloqueos = new();

    public async Task EjecutarAsync(
        long idConversacion,
        Func<CancellationToken, Task> accion,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim bloqueo = bloqueos.GetOrAdd(idConversacion, _ => new SemaphoreSlim(1, 1));

        await bloqueo.WaitAsync(cancellationToken);

        try
        {
            await accion(cancellationToken);
        }
        finally
        {
            bloqueo.Release();
        }
    }
}
