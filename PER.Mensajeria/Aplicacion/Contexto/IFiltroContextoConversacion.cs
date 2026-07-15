namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IFiltroContextoConversacion
{
    Task<ResultadoFiltroContexto> EjecutarAsync(
        EstadoIteracionContextoConversacion estado,
        CancellationToken cancellationToken);
}
