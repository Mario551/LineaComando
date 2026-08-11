namespace PER.Mensajeria.Builder.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Mensaje;

public class MensajeriaWorker : BackgroundService
{
    private const string EstadoEnviado = "enviado";
    private const string EstadoFallido = "fallido";

    private static readonly TimeSpan EsperaReintento = TimeSpan.FromSeconds(5);

    private readonly IRecepcionMensajeriaAPI recepcionMensajeriaAPI;
    private readonly IEnvioMensajeriaAPI? envioMensajeriaAPI;
    private readonly IMensajeServicio mensajeServicio;
    private readonly IColaEventosMensajeriaSalidaServicio colaEventosMensajeriaSalidaServicio;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<MensajeriaWorker> logger;

    public MensajeriaWorker(
        IRecepcionMensajeriaAPI recepcionMensajeriaAPI,
        IEnumerable<IEnvioMensajeriaAPI> enviosMensajeriaAPI,
        IMensajeServicio mensajeServicio,
        IColaEventosMensajeriaSalidaServicio colaEventosMensajeriaSalidaServicio,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MensajeriaWorker> logger)
    {
        this.recepcionMensajeriaAPI = recepcionMensajeriaAPI;
        List<IEnvioMensajeriaAPI> envios = enviosMensajeriaAPI.Distinct().ToList();
        if (envios.Count > 1)
        {
            throw new InvalidOperationException(
                "Solo puede existir una comunicacion de salida de mensajeria registrada.");
        }

        envioMensajeriaAPI = envios.SingleOrDefault();
        this.mensajeServicio = mensajeServicio;
        this.colaEventosMensajeriaSalidaServicio = colaEventosMensajeriaSalidaServicio;
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
    }

    public async Task EjecutarEntradaAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DTORegistrarMensajeEntranteSolicitud? solicitud = null;

            try
            {
                solicitud = await recepcionMensajeriaAPI.EsperarMensajeEntranteAsync(
                    cancellationToken);
                ArgumentNullException.ThrowIfNull(solicitud);

                await RegistrarEntradaConReintentoAsync(solicitud, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excepcion)
            {
                logger.LogError(
                    excepcion,
                    "Error esperando un mensaje entrante desde la comunicacion externa.");
                await Task.Delay(EsperaReintento, cancellationToken);
            }
        }
    }

    public async Task EjecutarSalidaAsync(CancellationToken cancellationToken)
    {
        IEnvioMensajeriaAPI envio = ObtenerEnvioMensajeria();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                DTOEnvioMensajePendiente mensaje = await mensajeServicio.EsperarMensajeSalidaAsync(
                    cancellationToken);
                await ProcesarSalidaAsync(envio, mensaje, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excepcion)
            {
                logger.LogError(
                    excepcion,
                    "Error no controlado en el ciclo de salida de mensajeria.");
                await Task.Delay(EsperaReintento, cancellationToken);
            }
        }
    }

    public async Task ProcesarSalidaAsync(
        DTOEnvioMensajePendiente mensaje,
        CancellationToken cancellationToken)
    {
        await ProcesarSalidaAsync(ObtenerEnvioMensajeria(), mensaje, cancellationToken);
    }

    private async Task ProcesarSalidaAsync(
        IEnvioMensajeriaAPI envio,
        DTOEnvioMensajePendiente mensaje,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        DTOResultadoEnvioMensaje resultado;

        try
        {
            resultado = await envio.EnviarMensajeAsync(
                mensaje,
                cancellationToken);
            ArgumentNullException.ThrowIfNull(resultado);

            if (resultado.IDEnvioMensaje != mensaje.IDEnvioMensaje)
            {
                throw new InvalidOperationException(
                    $"La comunicacion devolvio el envio {resultado.IDEnvioMensaje} para el envio esperado {mensaje.IDEnvioMensaje}.");
            }

            if (resultado.Estado is not EstadoEnviado and not EstadoFallido)
            {
                resultado = CrearResultadoFallido(
                    mensaje.IDEnvioMensaje,
                    $"La comunicacion devolvio el estado no soportado '{resultado.Estado}'.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Error enviando mensaje por la comunicacion externa. IDEnvioMensaje={IDEnvioMensaje}",
                mensaje.IDEnvioMensaje);
            resultado = CrearResultadoFallido(mensaje.IDEnvioMensaje, excepcion.Message);
        }

        try
        {
            await mensajeServicio.RegistrarResultadoEnvioAsync(resultado, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "No se pudo confirmar el resultado del envio. Se volvera a publicar. IDEnvioMensaje={IDEnvioMensaje}",
                mensaje.IDEnvioMensaje);
            colaEventosMensajeriaSalidaServicio.Publicar(new EventoMensajeriaSalida
            {
                IDEnvioMensaje = mensaje.IDEnvioMensaje,
                FechaCreacion = DateTime.Now
            });
            await Task.Delay(EsperaReintento, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Task tareaEntrada = EjecutarEntradaAsync(stoppingToken);
            if (envioMensajeriaAPI is null)
            {
                await tareaEntrada;
                return;
            }

            await Task.WhenAll(tareaEntrada, EjecutarSalidaConCargaInicialAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task EjecutarSalidaConCargaInicialAsync(CancellationToken cancellationToken)
    {
        await CargarSalidasPendientesConReintentoAsync(cancellationToken);
        await EjecutarSalidaAsync(cancellationToken);
    }

    private async Task RegistrarEntradaConReintentoAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                DTORegistrarMensajeEntranteRespuesta resultado = await mensajeServicio.RecibirAsync(
                    solicitud,
                    cancellationToken);

                if (recepcionMensajeriaAPI is IConfirmacionMensajeEntranteAPI confirmacion)
                {
                    await confirmacion.ConfirmarMensajeEntranteAsync(
                        solicitud,
                        resultado,
                        cancellationToken);
                }

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excepcion)
            {
                logger.LogError(
                    excepcion,
                    "Error registrando mensaje entrante. Se reintentara el mismo mensaje.");
                await Task.Delay(EsperaReintento, cancellationToken);
            }
        }
    }

    private async Task CargarSalidasPendientesConReintentoAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CargarSalidasPendientesAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excepcion)
            {
                logger.LogError(
                    excepcion,
                    "Error cargando salidas pendientes. Se reintentara en {SegundosReintento} segundos.",
                    EsperaReintento.TotalSeconds);
                await Task.Delay(EsperaReintento, cancellationToken);
            }
        }
    }

    public async Task CargarSalidasPendientesAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope alcance = serviceScopeFactory.CreateAsyncScope();
        ICargarEventosMensajeriaSalidaPendientesAplicacion cargarEventosPendientes = alcance.ServiceProvider
            .GetRequiredService<ICargarEventosMensajeriaSalidaPendientesAplicacion>();
        List<EventoMensajeriaSalida> eventos = await cargarEventosPendientes.EjecutarAsync(
            cancellationToken);

        foreach (EventoMensajeriaSalida evento in eventos)
        {
            colaEventosMensajeriaSalidaServicio.PublicarRehidratado(evento);
        }

        logger.LogInformation(
            "Finaliza carga inicial de salidas pendientes. Eventos={CantidadEventos}",
            eventos.Count);
    }

    private static DTOResultadoEnvioMensaje CrearResultadoFallido(
        long idEnvioMensaje,
        string error)
    {
        return new DTOResultadoEnvioMensaje
        {
            IDEnvioMensaje = idEnvioMensaje,
            Estado = EstadoFallido,
            Error = string.IsNullOrWhiteSpace(error)
                ? "Error al enviar mensaje."
                : error
        };
    }

    private IEnvioMensajeriaAPI ObtenerEnvioMensajeria()
    {
        return envioMensajeriaAPI
            ?? throw new InvalidOperationException(
                "No existe una comunicacion de salida de mensajeria registrada.");
    }
}
