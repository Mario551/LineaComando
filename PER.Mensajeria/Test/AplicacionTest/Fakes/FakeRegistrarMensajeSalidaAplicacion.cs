using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

namespace AplicacionTest.Fakes;

public class FakeRegistrarMensajeSalidaAplicacion : IRegistrarMensajeSalidaAplicacion
{
    public bool Ejecutado { get; private set; }

    public Task<ResultadoRegistrarMensajeSalida> EjecutarAsync(
        SolicitudRegistrarMensajeSalida solicitud,
        CancellationToken cancellationToken)
    {
        Ejecutado = true;

        return Task.FromResult(new ResultadoRegistrarMensajeSalida
        {
            IDMensaje = 10,
            IDEnvioMensaje = 20,
            Registrado = true
        });
    }
}
