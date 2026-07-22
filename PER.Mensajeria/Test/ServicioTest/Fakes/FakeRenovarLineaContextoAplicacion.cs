using PER.Mensajeria.Aplicacion.RenovarLineaContexto;

namespace ServicioTest.Fakes;

public class FakeRenovarLineaContextoAplicacion : IRenovarLineaContextoAplicacion
{
    public SolicitudRenovarLineaContexto? Solicitud { get; private set; }

    public Task<ResultadoRenovarLineaContexto> EjecutarAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken)
    {
        Solicitud = solicitud;

        return Task.FromResult(new ResultadoRenovarLineaContexto
        {
            IDCompactacionContexto = 5,
            IDLineaConversacion = 6,
            IDMensaje = solicitud.IDMensaje,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDConversacion = solicitud.IDConversacion
        });
    }
}
