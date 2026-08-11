using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.Envio;

public interface IRegistrarIntentoEnvioInfobipAplicacion
{
    Task<long> IniciarAsync(
        long idEnvioMensaje,
        DTOInfobipSolicitudEnvio solicitud,
        CancellationToken cancellationToken);

    Task RegistrarFalloAdaptacionAsync(
        long idEnvioMensaje,
        string error,
        CancellationToken cancellationToken);

    Task FinalizarAsync(
        long idIntento,
        string estado,
        DTOResultadoEnvioInfobipCliente resultado,
        string? error,
        CancellationToken cancellationToken);

    Task MarcarInciertoAsync(
        long idIntento,
        string error,
        CancellationToken cancellationToken);
}
