namespace PER.Mensajeria.Aplicacion.EnviarMensaje;

using PER.Mensajeria.Entidad.DTO;

public interface IEnviarMensajeAplicacion
{
    Task<DTOResultadoEnvioMensaje> EjecutarAsync(long idEnvioMensaje, CancellationToken cancellationToken);
}
