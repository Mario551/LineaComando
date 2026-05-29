namespace PER.Mensajeria.Servicio.Envio;

using PER.Mensajeria.Aplicacion.EnviarMensaje;

public class EnvioMensajeServicio : IEnvioMensajeServicio
{
    private readonly IEnviarMensajeAplicacion enviarMensajeAplicacion;

    public EnvioMensajeServicio(IEnviarMensajeAplicacion enviarMensajeAplicacion)
    {
        this.enviarMensajeAplicacion = enviarMensajeAplicacion;
    }

    public Task ProcesarAsync(CancellationToken cancellationToken)
    {
        return enviarMensajeAplicacion.EjecutarAsync(0, cancellationToken);
    }
}
