namespace PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;

using PER.Mensajeria.Entidad.DTO;

public interface IRegistrarMensajeEntranteAplicacion
{
    Task<DTORegistrarMensajeEntranteRespuesta> EjecutarAsync(DTORegistrarMensajeEntranteSolicitud solicitud, CancellationToken cancellationToken);
}
