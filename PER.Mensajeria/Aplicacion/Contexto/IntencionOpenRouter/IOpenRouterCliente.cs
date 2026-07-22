using PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public interface IOpenRouterCliente
{
    Task<ResultadoOpenRouterCliente> CompletarChatAsync(
        DTOOpenRouterSolicitudChat solicitud,
        CancellationToken cancellationToken);
}
