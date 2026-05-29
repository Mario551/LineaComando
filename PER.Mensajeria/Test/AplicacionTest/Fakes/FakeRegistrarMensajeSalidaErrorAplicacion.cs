using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest.Fakes;

public class FakeRegistrarMensajeSalidaErrorAplicacion : IRegistrarMensajeSalidaAplicacion
{
    public Task<DTORegistrarMensajeSalidaRespuesta> EjecutarAsync(DTORegistrarMensajeSalidaSolicitud solicitud, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Fallo contexto fake.");
    }
}
