using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Entidad.DTO;

namespace ServicioTest.Fakes;

public class FakeRegistrarMensajeEntranteAplicacion : IRegistrarMensajeEntranteAplicacion
{
    public bool Ejecutado { get; private set; }
    public DTORegistrarMensajeEntranteSolicitud? Solicitud { get; private set; }
    public DTORegistrarMensajeEntranteRespuesta Respuesta { get; set; } = new()
    {
        IDMensaje = 1,
        IDConversacion = 2,
        IDLineaConversacion = 3,
        IDProcesamientoInternoMensaje = 4,
        Registrado = true
    };

    public Task<DTORegistrarMensajeEntranteRespuesta> EjecutarAsync(DTORegistrarMensajeEntranteSolicitud solicitud, CancellationToken cancellationToken)
    {
        Ejecutado = true;
        Solicitud = solicitud;

        return Task.FromResult(Respuesta);
    }
}
