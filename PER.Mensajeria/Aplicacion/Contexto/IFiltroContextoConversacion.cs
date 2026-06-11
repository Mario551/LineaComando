namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IFiltroContextoConversacion
{
    Task<ResultadoFiltroContexto> EjecutarAsync(
        EstadoContextoConversacion estado,
        CancellationToken cancellationToken);
}
