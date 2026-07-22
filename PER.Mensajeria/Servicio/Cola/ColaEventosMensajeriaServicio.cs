using System.Collections.Concurrent;

namespace PER.Mensajeria.Servicio.Cola;

public class ColaEventosMensajeriaServicio : IColaEventosMensajeriaServicio
{
    private readonly ConcurrentQueue<EventoMensajeria> eventosMensajeriaPrioritarios = new();
    private readonly ConcurrentQueue<EventoMensajeria> eventosMensajeria = new();
    private readonly ConcurrentDictionary<long, byte> procesamientosEnCola = new();
    private readonly SemaphoreSlim eventosDisponibles = new(0);

    public void Publicar(EventoMensajeria eventoMensajeria)
    {
        Publicar(eventoMensajeria, eventosMensajeria);
    }

    public void PublicarRehidratado(EventoMensajeria eventoMensajeria)
    {
        Publicar(eventoMensajeria, eventosMensajeriaPrioritarios);
    }

    private void Publicar(
        EventoMensajeria eventoMensajeria,
        ConcurrentQueue<EventoMensajeria> colaDestino)
    {
        if (!procesamientosEnCola.TryAdd(eventoMensajeria.IDProcesamientoInternoMensaje, 0))
        {
            return;
        }

        colaDestino.Enqueue(eventoMensajeria);
        eventosDisponibles.Release();
    }

    public async Task<EventoMensajeria> ConsumirAsync(CancellationToken cancellationToken)
    {
        await eventosDisponibles.WaitAsync(cancellationToken);
        if (!eventosMensajeriaPrioritarios.TryDequeue(out EventoMensajeria? eventoMensajeria))
        {
            eventosMensajeria.TryDequeue(out eventoMensajeria);
        }

        if (eventoMensajeria is null)
        {
            throw new InvalidOperationException("No se pudo consumir el evento de mensajeria.");
        }

        procesamientosEnCola.TryRemove(eventoMensajeria.IDProcesamientoInternoMensaje, out _);
        return eventoMensajeria;
    }
}
