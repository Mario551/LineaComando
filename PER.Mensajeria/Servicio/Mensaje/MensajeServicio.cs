namespace PER.Mensajeria.Servicio.Mensaje;

using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Cola;

public class MensajeServicio : IMensajeServicio
{
    private readonly IRegistrarMensajeEntranteAplicacion registrarMensajeEntranteAplicacion;
    private readonly IColaEventosMensajeriaServicio colaEventosMensajeriaServicio;

    public MensajeServicio(
        IRegistrarMensajeEntranteAplicacion registrarMensajeEntranteAplicacion,
        IColaEventosMensajeriaServicio colaEventosMensajeriaServicio)
    {
        this.registrarMensajeEntranteAplicacion = registrarMensajeEntranteAplicacion;
        this.colaEventosMensajeriaServicio = colaEventosMensajeriaServicio;
    }

    public async Task<DTORegistrarMensajeEntranteRespuesta> RecibirAsync(DTORegistrarMensajeEntranteSolicitud solicitud, CancellationToken cancellationToken)
    {
        DTORegistrarMensajeEntranteRespuesta respuesta = await registrarMensajeEntranteAplicacion.EjecutarAsync(solicitud, cancellationToken);

        if (respuesta.Registrado)
        {
            colaEventosMensajeriaServicio.Publicar(new EventoMensajeria
            {
                IDMensaje = respuesta.IDMensaje,
                IDProcesamientoInternoMensaje = respuesta.IDProcesamientoInternoMensaje,
                IDConversacion = respuesta.IDConversacion,
                IDLineaConversacion = respuesta.IDLineaConversacion,
                FechaCreacion = DateTime.Now
            });
        }

        return respuesta;
    }
}
