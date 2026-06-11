namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public interface IContextoConversacionServicio
{
    Task<ResultadoContextoConversacion> ResolverAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken);
}
