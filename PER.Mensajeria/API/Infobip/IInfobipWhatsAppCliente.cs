using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.API.Infobip;

public interface IInfobipWhatsAppCliente
{
    Task<DTOResultadoEnvioInfobipCliente> EnviarAsync(
        DTOInfobipSolicitudEnvio solicitud,
        CancellationToken cancellationToken);
}
