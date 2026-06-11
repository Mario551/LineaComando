namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public interface IProveedorHistorialContextoServicio
{
    Task<ResultadoHistorialContexto> ObtenerAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken);
}
