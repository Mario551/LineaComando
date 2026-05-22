namespace PER.Mensajeria.Servicio.Envio;

public interface IEnvioMensajeServicio
{
    Task ProcesarAsync(CancellationToken cancellationToken);
}
