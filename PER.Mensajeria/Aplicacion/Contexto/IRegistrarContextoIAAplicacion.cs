namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

public interface IRegistrarContextoIAAplicacion
{
    Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerMetadataEntradasProcesamientoAsync(
        long idLineaConversacion,
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken);

    Task<ResultadoRegistrarDecisionContextoIA> RegistrarDecisionAsync(
        SolicitudContextoConversacion solicitud,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        SolicitudRegistrarMetadataEntradaContextoIA entrada,
        SolicitudPrepararEjecucionComandoContexto? preparacionEjecucion,
        CancellationToken cancellationToken);

    Task<MetadataEntradaContextoIA> RegistrarMetadataResultadoComandoAsync(
        long idEjecucionComandoContexto,
        SolicitudRegistrarMetadataEntradaContextoIA entrada,
        ResultadoComandoContexto resultadoComando,
        CancellationToken cancellationToken);

    Task<MetadataEntradaContextoIA> RegistrarMetadataEntradaAsync(
        SolicitudRegistrarMetadataEntradaContextoIA solicitud,
        CancellationToken cancellationToken);
}
