namespace PER.Mensajeria.API.Canal;

public class CanalMensajeAPI : ICanalMensajeAPI
{
    public Task EnviarAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
