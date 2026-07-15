namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IRegistrarContextoIAAplicacion
{
    Task<IReadOnlyList<EntradaContextoIA>> ObtenerEntradasAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken);

    Task<EntradaContextoIA> RegistrarDecisionAsync(
        SolicitudContextoConversacion solicitud,
        MetadataRazonamientoIAContexto metadata,
        SolicitudRegistrarEntradaContextoIA entrada,
        CancellationToken cancellationToken);

    Task<EntradaContextoIA> RegistrarEntradaAsync(
        SolicitudRegistrarEntradaContextoIA solicitud,
        CancellationToken cancellationToken);
}
