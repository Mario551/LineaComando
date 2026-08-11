namespace PER.Mensajeria.API.Comunicacion;

using PER.Mensajeria.Entidad.Infobip.DTO;

public interface IRecepcionWebhookInfobipAPI
{
    Task<DTOResultadoRecepcionWebhookInfobip> RecibirAsync(
        DTOInfobipWebhook webhook,
        CancellationToken cancellationToken);
}
