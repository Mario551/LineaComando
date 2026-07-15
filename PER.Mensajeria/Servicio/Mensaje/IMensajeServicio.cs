namespace PER.Mensajeria.Servicio.Mensaje;

using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Entidad.DTO;

public interface IMensajeServicio
{
    Task<DTORegistrarMensajeEntranteRespuesta> RecibirAsync(DTORegistrarMensajeEntranteSolicitud solicitud, CancellationToken cancellationToken);

    Task<ResultadoRenovarLineaContexto> RenovarLineaContextoAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken);
}
