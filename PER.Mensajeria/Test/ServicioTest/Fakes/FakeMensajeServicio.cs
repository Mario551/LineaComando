using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Mensaje;

namespace ServicioTest.Fakes;

public class FakeMensajeServicio : IMensajeServicio
{
    public SolicitudRenovarLineaContexto? SolicitudRenovacion { get; private set; }

    public Task<DTORegistrarMensajeEntranteRespuesta> RecibirAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<ResultadoRenovarLineaContexto> RenovarLineaContextoAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken)
    {
        SolicitudRenovacion = solicitud;

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
