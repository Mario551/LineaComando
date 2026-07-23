using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

namespace AplicacionTest.Fakes;

public class FakeRegistrarMensajeSalidaErrorAplicacion : IRegistrarMensajeSalidaAplicacion
{
    public Task<ResultadoRegistrarMensajeSalida> EjecutarAsync(
        SolicitudRegistrarMensajeSalida solicitud,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Fallo contexto fake.");
    }
}
