namespace PER.Mensajeria.Servicio.Envio;

public class EnvioMensajeServicio : IEnvioMensajeServicio
{
    public Task ProcesarAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
