namespace PER.Mensajeria.API.Contexto;

public interface IFiltroContextoConversacion
{
    Task<DTOResultadoFiltroContexto> EjecutarAsync(
        DTOContextoConversacionEstado estado,
        CancellationToken cancellationToken);
}
