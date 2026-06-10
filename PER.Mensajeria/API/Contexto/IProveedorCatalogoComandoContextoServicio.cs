namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public interface IProveedorCatalogoComandoContextoServicio
{
    Task<IReadOnlyList<DTOComandoContexto>> ObtenerAsync(
        DTOContextoConversacionSolicitud solicitud,
        CancellationToken cancellationToken);
}
