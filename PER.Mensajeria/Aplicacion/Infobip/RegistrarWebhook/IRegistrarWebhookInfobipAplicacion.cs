using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.RegistrarWebhook;

public interface IRegistrarWebhookInfobipAplicacion
{
    Task<DTOResultadoRecepcionMensajeInfobip> EjecutarAsync(
        DTOInfobipResult resultado,
        CancellationToken cancellationToken);
}
