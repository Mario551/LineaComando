using PER.Mensajeria.API.Canal;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest.Fakes;

public class FakeCanalMensajeAPI : ICanalMensajeAPI
{
    private readonly DTOResultadoEnvioMensaje resultado;

    public FakeCanalMensajeAPI(DTOResultadoEnvioMensaje resultado)
    {
        this.resultado = resultado;
    }

    public int CantidadLlamadas { get; private set; }
    public DTOMensajeSaliente? UltimoMensaje { get; private set; }

    public Task<DTOResultadoEnvioMensaje> EnviarAsync(DTOMensajeSaliente mensaje, CancellationToken cancellationToken)
    {
        CantidadLlamadas++;
        UltimoMensaje = mensaje;

        return Task.FromResult(resultado);
    }
}
