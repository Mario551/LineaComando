namespace PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;

using System.Collections.Concurrent;

public class ColaEventosMensajeriaSalidaServicio : IColaEventosMensajeriaSalidaServicio
{
    private readonly ConcurrentQueue<EventoMensajeriaSalida> eventosPrioritarios = new();
    private readonly ConcurrentQueue<EventoMensajeriaSalida> eventos = new();
    private readonly ConcurrentDictionary<long, byte> enviosEnCola = new();
    private readonly SemaphoreSlim eventosDisponibles = new(0);

    public void Publicar(EventoMensajeriaSalida eventoMensajeria)
    {
        Publicar(eventoMensajeria, eventos);
    }

    public void PublicarRehidratado(EventoMensajeriaSalida eventoMensajeria)
    {
        Publicar(eventoMensajeria, eventosPrioritarios);
    }

    public async Task<EventoMensajeriaSalida> ConsumirAsync(CancellationToken cancellationToken)
    {
        await eventosDisponibles.WaitAsync(cancellationToken);

        if (!eventosPrioritarios.TryDequeue(out EventoMensajeriaSalida? eventoMensajeria))
        {
            eventos.TryDequeue(out eventoMensajeria);
        }

        if (eventoMensajeria is null)
        {
            throw new InvalidOperationException("No se pudo consumir el evento de salida de mensajeria.");
        }

        enviosEnCola.TryRemove(eventoMensajeria.IDEnvioMensaje, out _);
        return eventoMensajeria;
    }

    private void Publicar(
        EventoMensajeriaSalida eventoMensajeria,
        ConcurrentQueue<EventoMensajeriaSalida> colaDestino)
    {
        ArgumentNullException.ThrowIfNull(eventoMensajeria);

        if (!enviosEnCola.TryAdd(eventoMensajeria.IDEnvioMensaje, 0))
        {
            return;
        }

        colaDestino.Enqueue(eventoMensajeria);
        eventosDisponibles.Release();
    }
}
