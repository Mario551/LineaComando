namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IConsultaMensajesLineaConversacionAnteriorAplicacion
{
    Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerCicloAsync(
        long idConversacion,
        long idLineaConversacionActual,
        int ciclosHaciaAtras,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerCicloReferenciadoAsync(
        long idConversacion,
        long idLineaConversacionActual,
        long idLineaConversacionOrigen,
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken);
}
