namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public interface IProveedorCatalogoComandoContextoServicio
{
    Task<IReadOnlyList<ComandoContexto>> ObtenerAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken);
}
