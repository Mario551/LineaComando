namespace PER.Mensajeria.API.Comunicacion;

using PER.Mensajeria.Entidad.DTO;

public interface IConfirmacionMensajeEntranteAPI
{
    Task ConfirmarMensajeEntranteAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        DTORegistrarMensajeEntranteRespuesta resultado,
        CancellationToken cancellationToken);
}
