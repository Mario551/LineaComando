namespace PER.Mensajeria.Aplicacion.Contexto;

public interface ICompactacionContextoConversacionAplicacion
{
    Task<CompactacionContextoConversacion?> ObtenerInicialAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken);
}
