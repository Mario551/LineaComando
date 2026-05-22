namespace PER.Mensajeria.Servicio.Mensaje;

public interface IMensajeServicio
{
    Task RecibirAsync(CancellationToken cancellationToken);
}
