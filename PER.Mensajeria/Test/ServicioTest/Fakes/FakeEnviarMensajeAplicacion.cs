using PER.Mensajeria.Aplicacion.EnviarMensaje;
using PER.Mensajeria.Entidad.DTO;

namespace ServicioTest.Fakes;

public class FakeEnviarMensajeAplicacion : IEnviarMensajeAplicacion
{
    public bool Ejecutado { get; private set; }

    public Task<DTOResultadoEnvioMensaje> EjecutarAsync(long idEnvioMensaje, CancellationToken cancellationToken)
    {
        Ejecutado = true;

        return Task.FromResult(new DTOResultadoEnvioMensaje
        {
            IDEnvioMensaje = idEnvioMensaje,
            Estado = "enviado"
        });
    }
}
