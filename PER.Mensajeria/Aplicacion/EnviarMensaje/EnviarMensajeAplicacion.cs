namespace PER.Mensajeria.Aplicacion.EnviarMensaje;

public class EnviarMensajeAplicacion : IEnviarMensajeAplicacion
{
    public Task EjecutarAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
