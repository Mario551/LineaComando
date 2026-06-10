namespace PER.Mensajeria.API.Contexto;

public interface IIntencionContextoConversacionServicio
{
    Task<DTOIntencionContextoResultado> DecidirAsync(
        DTOIntencionContextoSolicitud solicitud,
        CancellationToken cancellationToken);
}
