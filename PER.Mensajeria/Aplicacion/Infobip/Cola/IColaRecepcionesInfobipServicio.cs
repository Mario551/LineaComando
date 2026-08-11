namespace PER.Mensajeria.Aplicacion.Infobip.Cola;

public interface IColaRecepcionesInfobipServicio
{
    void Publicar(long idWebhookReceiptInfobip);
    void PublicarRehidratado(long idWebhookReceiptInfobip);
    Task<long> ConsumirAsync(CancellationToken cancellationToken);
}
