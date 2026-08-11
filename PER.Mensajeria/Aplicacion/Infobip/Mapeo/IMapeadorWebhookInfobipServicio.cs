using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.Mapeo;

public interface IMapeadorWebhookInfobipServicio
{
    WebhookReceiptInfobip Mapear(
        DTOInfobipResult resultado,
        DateTime fechaCreacion);
}
