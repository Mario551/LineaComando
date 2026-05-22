namespace PER.Mensajeria.Servicio.Mensaje;

public class MensajeServicio : IMensajeServicio
{
    public Task RecibirAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
