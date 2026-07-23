namespace PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

using System.Collections.Concurrent;

public class ColaEventosMensajeriaEntradaServicio : IColaEventosMensajeriaEntradaServicio
{
    private readonly ConcurrentQueue<EventoMensajeriaEntrada> eventosPrioritarios = new();
    private readonly ConcurrentQueue<EventoMensajeriaEntrada> eventos = new();
    private readonly ConcurrentDictionary<long, byte> procesamientosEnCola = new();
    private readonly SemaphoreSlim eventosDisponibles = new(0);

    public void Publicar(EventoMensajeriaEntrada eventoMensajeria)
    {
        Publicar(eventoMensajeria, eventos);
    }

    public void PublicarRehidratado(EventoMensajeriaEntrada eventoMensajeria)
    {
        Publicar(eventoMensajeria, eventosPrioritarios);
    }

    public async Task<EventoMensajeriaEntrada> ConsumirAsync(CancellationToken cancellationToken)
    {
        await eventosDisponibles.WaitAsync(cancellationToken);

        if (!eventosPrioritarios.TryDequeue(out EventoMensajeriaEntrada? eventoMensajeria))
        {
            eventos.TryDequeue(out eventoMensajeria);
        }

        if (eventoMensajeria is null)
        {
            throw new InvalidOperationException("No se pudo consumir el evento de entrada de mensajeria.");
        }

        procesamientosEnCola.TryRemove(eventoMensajeria.IDProcesamientoInternoMensaje, out _);
        return eventoMensajeria;
    }

    private void Publicar(
        EventoMensajeriaEntrada eventoMensajeria,
        ConcurrentQueue<EventoMensajeriaEntrada> colaDestino)
    {
        ArgumentNullException.ThrowIfNull(eventoMensajeria);

        if (!procesamientosEnCola.TryAdd(eventoMensajeria.IDProcesamientoInternoMensaje, 0))
        {
            return;
        }

        colaDestino.Enqueue(eventoMensajeria);
        eventosDisponibles.Release();
    }
}
