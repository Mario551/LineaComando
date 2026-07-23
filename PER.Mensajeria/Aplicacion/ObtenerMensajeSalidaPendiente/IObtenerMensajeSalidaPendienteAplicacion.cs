namespace PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;

using PER.Mensajeria.Entidad.DTO;

public interface IObtenerMensajeSalidaPendienteAplicacion
{
    Task<DTOEnvioMensajePendiente?> EjecutarAsync(
        long idEnvioMensaje,
        CancellationToken cancellationToken);
}
