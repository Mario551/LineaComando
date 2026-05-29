namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

using PER.Mensajeria.Entidad.DTO;

public interface IRegistrarMensajeSalidaAplicacion
{
    Task<DTORegistrarMensajeSalidaRespuesta> EjecutarAsync(DTORegistrarMensajeSalidaSolicitud solicitud, CancellationToken cancellationToken);
}
