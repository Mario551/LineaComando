using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Repositorio.Infobip;

public interface IWebhookReceiptInfobipRepositorio : IRepositorio<WebhookReceiptInfobip>
{
    Task<WebhookReceiptInfobip?> ObtenerAgregadoNoTrackingAsync(
        long idWebhookReceiptInfobip,
        CancellationToken cancellationToken);
}
