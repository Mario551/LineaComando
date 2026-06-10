namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public interface IContextoConversacionServicio
{
    Task<DTOResultadoContextoConversacion> ResolverAsync(
        DTOContextoConversacionSolicitud solicitud,
        CancellationToken cancellationToken);
}
