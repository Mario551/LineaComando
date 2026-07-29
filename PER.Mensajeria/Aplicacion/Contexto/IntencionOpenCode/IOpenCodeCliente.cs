using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public interface IOpenCodeCliente
{
    Task<ResultadoOpenCodeCliente<DTOOpenCodeSesion>> CrearSesionAsync(
        DTOOpenCodeCrearSesionSolicitud solicitud,
        CancellationToken cancellationToken);

    Task<ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>> EnviarMensajeAsync(
        string idSesion,
        DTOOpenCodeMensajeSolicitud solicitud,
        CancellationToken cancellationToken);

    Task<ResultadoOpenCodeCliente<bool>> AbortarSesionAsync(
        string idSesion,
        CancellationToken cancellationToken);

    Task<ResultadoOpenCodeCliente<bool>> EliminarSesionAsync(
        string idSesion,
        CancellationToken cancellationToken);
}
