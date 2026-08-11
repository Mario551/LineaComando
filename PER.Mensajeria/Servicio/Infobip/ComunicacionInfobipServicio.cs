using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.Aplicacion.Infobip.CargarPendientes;
using PER.Mensajeria.Aplicacion.Infobip.Cola;
using PER.Mensajeria.Aplicacion.Infobip.ConfirmarMensajeEntrante;
using PER.Mensajeria.Aplicacion.Infobip.Envio;
using PER.Mensajeria.Aplicacion.Infobip.ObtenerMensajeEntrante;
using PER.Mensajeria.Aplicacion.Infobip.RegistrarWebhook;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Servicio.Infobip;

public class ComunicacionInfobipServicio :
    IRecepcionWebhookInfobipAPI,
    IRecepcionMensajeriaAPI,
    IConfirmacionMensajeEntranteAPI,
    IEnvioMensajeriaAPI
{
    private const string EstadoAceptado = "aceptado";
    private const string EstadoFallido = "fallido";
    private const string EstadoIncierto = "incierto";
    private const string EstadoGenericoEnviado = "enviado";
    private const string EstadoGenericoFallido = "fallido";

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IColaRecepcionesInfobipServicio colaRecepcionesInfobipServicio;
    private readonly ILogger<ComunicacionInfobipServicio> logger;
    private readonly SemaphoreSlim sincronizacionRehidratacion = new(1, 1);
    private volatile bool rehidratacionCompletada;

    public ComunicacionInfobipServicio(
        IServiceScopeFactory serviceScopeFactory,
        IColaRecepcionesInfobipServicio colaRecepcionesInfobipServicio,
        ILogger<ComunicacionInfobipServicio> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.colaRecepcionesInfobipServicio = colaRecepcionesInfobipServicio;
        this.logger = logger;
    }

    public async Task<DTOResultadoRecepcionWebhookInfobip> RecibirAsync(
        DTOInfobipWebhook webhook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webhook);
        DTOResultadoRecepcionWebhookInfobip respuesta = new();

        foreach (DTOInfobipResult resultado in webhook.Results ?? [])
        {
            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            IRegistrarWebhookInfobipAplicacion registrar = alcance.ServiceProvider
                .GetRequiredService<IRegistrarWebhookInfobipAplicacion>();
            DTOResultadoRecepcionMensajeInfobip resultadoRecepcion = await registrar.EjecutarAsync(
                resultado,
                cancellationToken);
            respuesta.Resultados.Add(resultadoRecepcion);

            if (resultadoRecepcion.IDWebhookReceiptInfobip > 0
                && resultadoRecepcion.Estado is "pendiente" or "despachado")
            {
                colaRecepcionesInfobipServicio.Publicar(
                    resultadoRecepcion.IDWebhookReceiptInfobip);
            }
        }

        return respuesta;
    }

    public async Task<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
        CancellationToken cancellationToken)
    {
        await AsegurarRehidratacionAsync(cancellationToken);

        while (true)
        {
            long idWebhookReceiptInfobip = await colaRecepcionesInfobipServicio.ConsumirAsync(
                cancellationToken);
            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            IObtenerMensajeEntranteInfobipAplicacion obtener = alcance.ServiceProvider
                .GetRequiredService<IObtenerMensajeEntranteInfobipAplicacion>();
            DTORegistrarMensajeEntranteSolicitud? solicitud = await obtener.EjecutarAsync(
                idWebhookReceiptInfobip,
                cancellationToken);

            if (solicitud is not null)
            {
                return solicitud;
            }

            logger.LogInformation(
                "La recepcion Infobip {IDWebhookReceiptInfobip} no se publico al flujo generico.",
                idWebhookReceiptInfobip);
        }
    }

    public async Task ConfirmarMensajeEntranteAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        DTORegistrarMensajeEntranteRespuesta resultado,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
        IConfirmarMensajeEntranteInfobipAplicacion confirmar = alcance.ServiceProvider
            .GetRequiredService<IConfirmarMensajeEntranteInfobipAplicacion>();
        await confirmar.EjecutarAsync(solicitud, resultado, cancellationToken);
    }

    public async Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
        DTOEnvioMensajePendiente mensaje,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        DTOResultadoAdaptacionEnvioInfobip adaptacion;
        long idIntento;

        await using (AsyncServiceScope alcancePreparacion =
            serviceScopeFactory.CreateAsyncScope())
        {
            IAdaptadorMensajeSalidaInfobip adaptador = alcancePreparacion.ServiceProvider
                .GetRequiredService<IAdaptadorMensajeSalidaInfobip>();
            IRegistrarIntentoEnvioInfobipAplicacion registrar = alcancePreparacion.ServiceProvider
                .GetRequiredService<IRegistrarIntentoEnvioInfobipAplicacion>();
            adaptacion = adaptador.Adaptar(mensaje);

            if (!adaptacion.Exitosa || adaptacion.Solicitud is null)
            {
                string errorAdaptacion = adaptacion.Error
                    ?? "No fue posible adaptar el mensaje para Infobip.";
                await registrar.RegistrarFalloAdaptacionAsync(
                    mensaje.IDEnvioMensaje,
                    errorAdaptacion,
                    cancellationToken);
                return CrearResultadoFallido(mensaje.IDEnvioMensaje, errorAdaptacion);
            }

            idIntento = await registrar.IniciarAsync(
                mensaje.IDEnvioMensaje,
                adaptacion.Solicitud,
                cancellationToken);
        }

        DTOResultadoEnvioInfobipCliente resultadoCliente;

        try
        {
            await using AsyncServiceScope alcanceHttp = serviceScopeFactory.CreateAsyncScope();
            IInfobipWhatsAppCliente cliente = alcanceHttp.ServiceProvider
                .GetRequiredService<IInfobipWhatsAppCliente>();
            resultadoCliente = await cliente.EnviarAsync(
                adaptacion.Solicitud,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarcarInciertoConLimpiezaAsync(
                idIntento,
                "El host canceló la operación mientras se enviaba el mensaje a Infobip.");
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Error no controlado llamando a Infobip. IDEnvioMensaje={IDEnvioMensaje}, IDIntento={IDIntento}",
                mensaje.IDEnvioMensaje,
                idIntento);
            resultadoCliente = new DTOResultadoEnvioInfobipCliente
            {
                EsResultadoIncierto = true,
                ErrorTecnico = excepcion.Message
            };
        }

        bool aceptado = EsRespuestaAceptada(resultadoCliente);
        bool respuestaExitosaIncompleta = EsRespuestaExitosaIncompleta(resultadoCliente);
        string estadoIntento = aceptado
            ? EstadoAceptado
            : resultadoCliente.EsResultadoIncierto || respuestaExitosaIncompleta
                ? EstadoIncierto
                : EstadoFallido;
        string? error = aceptado ? null : ObtenerError(resultadoCliente);

        await using (AsyncServiceScope alcanceFinalizacion =
            serviceScopeFactory.CreateAsyncScope())
        {
            IRegistrarIntentoEnvioInfobipAplicacion registrar = alcanceFinalizacion.ServiceProvider
                .GetRequiredService<IRegistrarIntentoEnvioInfobipAplicacion>();
            await registrar.FinalizarAsync(
                idIntento,
                estadoIntento,
                resultadoCliente,
                error,
                cancellationToken);
        }

        return aceptado
            ? new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = mensaje.IDEnvioMensaje,
                Estado = EstadoGenericoEnviado
            }
            : CrearResultadoFallido(mensaje.IDEnvioMensaje, error);
    }

    private async Task AsegurarRehidratacionAsync(CancellationToken cancellationToken)
    {
        if (rehidratacionCompletada)
        {
            return;
        }

        await sincronizacionRehidratacion.WaitAsync(cancellationToken);

        try
        {
            if (rehidratacionCompletada)
            {
                return;
            }

            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            ICargarRecepcionesInfobipPendientesAplicacion cargar = alcance.ServiceProvider
                .GetRequiredService<ICargarRecepcionesInfobipPendientesAplicacion>();
            List<long> recepciones = await cargar.EjecutarAsync(cancellationToken);

            foreach (long idWebhookReceiptInfobip in recepciones)
            {
                colaRecepcionesInfobipServicio.PublicarRehidratado(idWebhookReceiptInfobip);
            }

            rehidratacionCompletada = true;
            logger.LogInformation(
                "Finaliza rehidratacion de recepciones Infobip. Recepciones={CantidadRecepciones}",
                recepciones.Count);
        }
        finally
        {
            sincronizacionRehidratacion.Release();
        }
    }

    private async Task MarcarInciertoConLimpiezaAsync(
        long idIntento,
        string error)
    {
        using CancellationTokenSource limpieza = new(TimeSpan.FromSeconds(10));

        try
        {
            await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
            IRegistrarIntentoEnvioInfobipAplicacion registrar = alcance.ServiceProvider
                .GetRequiredService<IRegistrarIntentoEnvioInfobipAplicacion>();
            await registrar.MarcarInciertoAsync(
                idIntento,
                error,
                limpieza.Token);
        }
        catch (Exception excepcion)
        {
            logger.LogWarning(
                excepcion,
                "No fue posible marcar como incierto el intento Infobip {IDIntento} durante la cancelación.",
                idIntento);
        }
    }

    private static bool EsRespuestaAceptada(
        DTOResultadoEnvioInfobipCliente resultado)
    {
        DTOInfobipRespuestaEnvio? respuesta = resultado.Respuesta;
        DTOInfobipEstadoEnvio? estado = respuesta?.Status;
        return resultado.EsExitosoHttp
            && string.IsNullOrWhiteSpace(resultado.ErrorTecnico)
            && !string.IsNullOrWhiteSpace(respuesta?.MessageId)
            && estado?.GroupId == 1
            && string.Equals(estado.GroupName, "PENDING", StringComparison.OrdinalIgnoreCase)
            && estado.Name?.StartsWith("PENDING_", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool EsRespuestaExitosaIncompleta(
        DTOResultadoEnvioInfobipCliente resultado)
    {
        return resultado.EsExitosoHttp
            && (resultado.Respuesta is null
                || string.IsNullOrWhiteSpace(resultado.Respuesta.MessageId)
                || resultado.Respuesta.Status is null);
    }

    private static string ObtenerError(
        DTOResultadoEnvioInfobipCliente resultado)
    {
        if (!string.IsNullOrWhiteSpace(resultado.ErrorTecnico))
        {
            return resultado.ErrorTecnico;
        }

        if (!string.IsNullOrWhiteSpace(resultado.ErrorRespuesta?.Message))
        {
            return resultado.ErrorRespuesta.Message;
        }

        if (!string.IsNullOrWhiteSpace(resultado.ErrorRespuesta?.Error))
        {
            return resultado.ErrorRespuesta.Error;
        }

        if (!string.IsNullOrWhiteSpace(resultado.Respuesta?.Status?.Description))
        {
            return resultado.Respuesta.Status.Description;
        }

        return "Infobip no aceptó el mensaje para envío.";
    }

    private static DTOResultadoEnvioMensaje CrearResultadoFallido(
        long idEnvioMensaje,
        string? error)
    {
        return new DTOResultadoEnvioMensaje
        {
            IDEnvioMensaje = idEnvioMensaje,
            Estado = EstadoGenericoFallido,
            Error = string.IsNullOrWhiteSpace(error)
                ? "No fue posible enviar el mensaje por Infobip."
                : error
        };
    }
}
