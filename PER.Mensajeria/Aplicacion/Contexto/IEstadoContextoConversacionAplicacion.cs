namespace PER.Mensajeria.Aplicacion.Contexto;

public interface IEstadoContextoConversacionAplicacion
{
    Task<EstadoContextoConversacion?> ObtenerInicialAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken);
}
