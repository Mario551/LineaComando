using System.Collections.Concurrent;

namespace PER.Mensajeria.Aplicacion.Infobip.Cola;

public class ColaRecepcionesInfobipServicio : IColaRecepcionesInfobipServicio
{
    private readonly ConcurrentQueue<long> recepcionesPrioritarias = new();
    private readonly ConcurrentQueue<long> recepciones = new();
    private readonly ConcurrentDictionary<long, byte> recepcionesEnCola = new();
    private readonly SemaphoreSlim recepcionesDisponibles = new(0);

    public void Publicar(long idWebhookReceiptInfobip)
    {
        Publicar(idWebhookReceiptInfobip, recepciones);
    }

    public void PublicarRehidratado(long idWebhookReceiptInfobip)
    {
        Publicar(idWebhookReceiptInfobip, recepcionesPrioritarias);
    }

    public async Task<long> ConsumirAsync(CancellationToken cancellationToken)
    {
        await recepcionesDisponibles.WaitAsync(cancellationToken);

        if (!recepcionesPrioritarias.TryDequeue(out long idWebhookReceiptInfobip)
            && !recepciones.TryDequeue(out idWebhookReceiptInfobip))
        {
            throw new InvalidOperationException(
                "No se pudo consumir la recepcion Infobip anunciada.");
        }

        recepcionesEnCola.TryRemove(idWebhookReceiptInfobip, out _);
        return idWebhookReceiptInfobip;
    }

    private void Publicar(
        long idWebhookReceiptInfobip,
        ConcurrentQueue<long> colaDestino)
    {
        if (idWebhookReceiptInfobip <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(idWebhookReceiptInfobip),
                "El identificador de recepcion Infobip debe ser mayor que cero.");
        }

        if (!recepcionesEnCola.TryAdd(idWebhookReceiptInfobip, 0))
        {
            return;
        }

        colaDestino.Enqueue(idWebhookReceiptInfobip);
        recepcionesDisponibles.Release();
    }
}
