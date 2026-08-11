namespace PER.Mensajeria.API.Comunicacion;

using PER.Mensajeria.Entidad.DTO;

public interface IEnvioMensajeriaAPI
{
    Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
        DTOEnvioMensajePendiente mensaje,
        CancellationToken cancellationToken);
}
