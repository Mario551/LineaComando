namespace PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;

using PER.Mensajeria.Entidad.DTO;

public interface IRegistrarResultadoEnvioMensajeAplicacion
{
    Task EjecutarAsync(
        DTOResultadoEnvioMensaje resultado,
        CancellationToken cancellationToken);
}
