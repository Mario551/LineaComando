namespace PER.Mensajeria.API.Canal;

using PER.Mensajeria.Entidad.DTO;

public class CanalMensajeAPI : ICanalMensajeAPI
{
    public Task<DTOResultadoEnvioMensaje> EnviarAsync(DTOMensajeSaliente mensaje, CancellationToken cancellationToken)
    {
        return Task.FromResult(new DTOResultadoEnvioMensaje());
    }
}
