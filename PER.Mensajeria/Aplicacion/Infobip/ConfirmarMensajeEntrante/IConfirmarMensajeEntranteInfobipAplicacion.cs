using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.ConfirmarMensajeEntrante;

public interface IConfirmarMensajeEntranteInfobipAplicacion
{
    Task EjecutarAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        DTORegistrarMensajeEntranteRespuesta resultado,
        CancellationToken cancellationToken);
}
