using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest.Fakes;

public class FakeRegistrarMensajeSalidaAplicacion : IRegistrarMensajeSalidaAplicacion
{
    public bool Ejecutado { get; private set; }

    public Task<DTORegistrarMensajeSalidaRespuesta> EjecutarAsync(DTORegistrarMensajeSalidaSolicitud solicitud, CancellationToken cancellationToken)
    {
        Ejecutado = true;

        return Task.FromResult(new DTORegistrarMensajeSalidaRespuesta
        {
            IDMensaje = 10,
            IDEnvioMensaje = 20,
            Registrado = true
        });
    }
}
