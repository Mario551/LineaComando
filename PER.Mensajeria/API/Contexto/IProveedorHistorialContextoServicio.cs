namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public interface IProveedorHistorialContextoServicio
{
    Task<DTOResultadoHistorialContexto> ObtenerAsync(
        DTOContextoConversacionSolicitud solicitud,
        CancellationToken cancellationToken);
}
