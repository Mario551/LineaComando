namespace PER.Mensajeria.Aplicacion.EnviarMensaje;

public interface IEnviarMensajeAplicacion
{
    Task EjecutarAsync(CancellationToken cancellationToken);
}
