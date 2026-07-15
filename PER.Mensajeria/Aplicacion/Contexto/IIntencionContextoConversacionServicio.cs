namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IIntencionContextoConversacionServicio
{
    Task<ResultadoIntencionContexto> DecidirAsync(
        SolicitudIntencionContexto solicitud,
        CancellationToken cancellationToken);

    Task<ResultadoCompactacionIntencionContexto> CompactarAsync(
        SolicitudCompactacionIntencionContexto solicitud,
        CancellationToken cancellationToken);
}
