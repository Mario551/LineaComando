namespace PER.Mensajeria.API.Canal;

public interface ICanalMensajeAPI
{
    Task EnviarAsync(CancellationToken cancellationToken);
}
