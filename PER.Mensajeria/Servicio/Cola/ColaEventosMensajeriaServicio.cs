using System.Collections.Concurrent;

namespace PER.Mensajeria.Servicio.Cola;

public class ColaEventosMensajeriaServicio : IColaEventosMensajeriaServicio
{
    private readonly ConcurrentQueue<EventoMensajeria> eventosMensajeria = new();
    private readonly SemaphoreSlim eventosDisponibles = new(0);

    public void Publicar(EventoMensajeria eventoMensajeria)
    {
        eventosMensajeria.Enqueue(eventoMensajeria);
        eventosDisponibles.Release();
    }

    public async Task<EventoMensajeria> ConsumirAsync(CancellationToken cancellationToken)
    {
        await eventosDisponibles.WaitAsync(cancellationToken);
        eventosMensajeria.TryDequeue(out EventoMensajeria? eventoMensajeria);

        if (eventoMensajeria is null)
        {
            throw new InvalidOperationException("No se pudo consumir el evento de mensajeria.");
        }

        return eventoMensajeria;
    }
}
