namespace PER.Mensajeria.API.Canal;

using PER.Mensajeria.Entidad.DTO;

public interface ICanalMensajeAPI
{
    Task<DTOResultadoEnvioMensaje> EnviarAsync(DTOMensajeSaliente mensaje, CancellationToken cancellationToken);
}
