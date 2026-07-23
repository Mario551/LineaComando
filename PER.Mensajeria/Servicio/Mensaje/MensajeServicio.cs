namespace PER.Mensajeria.Servicio.Mensaje;

using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Entidad.DTO;

public class MensajeServicio : IMensajeServicio
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IColaEventosMensajeriaEntradaServicio colaEventosMensajeriaEntradaServicio;
    private readonly IColaEventosMensajeriaSalidaServicio colaEventosMensajeriaSalidaServicio;

    public MensajeServicio(
        IServiceScopeFactory serviceScopeFactory,
        IColaEventosMensajeriaEntradaServicio colaEventosMensajeriaEntradaServicio,
        IColaEventosMensajeriaSalidaServicio colaEventosMensajeriaSalidaServicio)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.colaEventosMensajeriaEntradaServicio = colaEventosMensajeriaEntradaServicio;
        this.colaEventosMensajeriaSalidaServicio = colaEventosMensajeriaSalidaServicio;
    }

    public async Task<DTORegistrarMensajeEntranteRespuesta> RecibirAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
        IRegistrarMensajeEntranteAplicacion registrarMensajeEntranteAplicacion = alcance.ServiceProvider
            .GetRequiredService<IRegistrarMensajeEntranteAplicacion>();
        DTORegistrarMensajeEntranteRespuesta respuesta = await registrarMensajeEntranteAplicacion.EjecutarAsync(solicitud, cancellationToken);

        if (respuesta.Registrado)
        {
            colaEventosMensajeriaEntradaServicio.Publicar(new EventoMensajeriaEntrada
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
        await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
        IRenovarLineaContextoAplicacion renovarLineaContextoAplicacion = alcance.ServiceProvider
            .GetRequiredService<IRenovarLineaContextoAplicacion>();
        ResultadoRenovarLineaContexto resultado = await renovarLineaContextoAplicacion.EjecutarAsync(
            solicitud,
            cancellationToken);

        colaEventosMensajeriaEntradaServicio.Publicar(new EventoMensajeriaEntrada
        {
            IDMensaje = resultado.IDMensaje,
            IDProcesamientoInternoMensaje = resultado.IDProcesamientoInternoMensaje,
            IDConversacion = resultado.IDConversacion,
            IDLineaConversacion = resultado.IDLineaConversacion,
            FechaCreacion = DateTime.Now
        });

        return resultado;
    }

    public async Task<DTOEnvioMensajePendiente> EsperarMensajeSalidaAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            EventoMensajeriaSalida evento = await colaEventosMensajeriaSalidaServicio.ConsumirAsync(
                cancellationToken);

            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            IObtenerMensajeSalidaPendienteAplicacion obtenerMensajeSalidaPendienteAplicacion = alcance.ServiceProvider
                .GetRequiredService<IObtenerMensajeSalidaPendienteAplicacion>();
            DTOEnvioMensajePendiente? mensaje = await obtenerMensajeSalidaPendienteAplicacion.EjecutarAsync(
                evento.IDEnvioMensaje,
                cancellationToken);

            if (mensaje is not null)
            {
                return mensaje;
            }
        }
    }

    public async Task RegistrarResultadoEnvioAsync(
        DTOResultadoEnvioMensaje resultado,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
        IRegistrarResultadoEnvioMensajeAplicacion registrarResultadoEnvioMensajeAplicacion = alcance.ServiceProvider
            .GetRequiredService<IRegistrarResultadoEnvioMensajeAplicacion>();
        await registrarResultadoEnvioMensajeAplicacion.EjecutarAsync(resultado, cancellationToken);
    }
}
