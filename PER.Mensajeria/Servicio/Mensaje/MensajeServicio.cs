namespace PER.Mensajeria.Servicio.Mensaje;

using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Cola;

public class MensajeServicio : IMensajeServicio
{
    private readonly IRegistrarMensajeEntranteAplicacion registrarMensajeEntranteAplicacion;
    private readonly IRenovarLineaContextoAplicacion renovarLineaContextoAplicacion;
    private readonly IColaEventosMensajeriaServicio colaEventosMensajeriaServicio;

    public MensajeServicio(
        IRegistrarMensajeEntranteAplicacion registrarMensajeEntranteAplicacion,
        IRenovarLineaContextoAplicacion renovarLineaContextoAplicacion,
        IColaEventosMensajeriaServicio colaEventosMensajeriaServicio)
    {
        this.registrarMensajeEntranteAplicacion = registrarMensajeEntranteAplicacion;
        this.renovarLineaContextoAplicacion = renovarLineaContextoAplicacion;
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

    public async Task<ResultadoRenovarLineaContexto> RenovarLineaContextoAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken)
    {
        ResultadoRenovarLineaContexto resultado = await renovarLineaContextoAplicacion.EjecutarAsync(
            solicitud,
            cancellationToken);

        colaEventosMensajeriaServicio.Publicar(new EventoMensajeria
        {
            IDMensaje = resultado.IDMensaje,
            IDProcesamientoInternoMensaje = resultado.IDProcesamientoInternoMensaje,
            IDConversacion = resultado.IDConversacion,
            IDLineaConversacion = resultado.IDLineaConversacion,
            FechaCreacion = DateTime.Now
        });

        return resultado;
    }
}
