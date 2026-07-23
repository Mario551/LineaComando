using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Servicio.Orquestador;

namespace PER.Mensajeria.Builder.Worker;

public class OrquestadorContextoWorker : BackgroundService
{
    private static readonly TimeSpan EsperaReintentoCarga = TimeSpan.FromSeconds(5);

    private readonly IColaEventosMensajeriaEntradaServicio colaEventosMensajeriaEntradaServicio;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IOrquestadorContextoServicio orquestadorContextoServicio;
    private readonly ILogger<OrquestadorContextoWorker> logger;

    public OrquestadorContextoWorker(
        IColaEventosMensajeriaEntradaServicio colaEventosMensajeriaEntradaServicio,
        IServiceScopeFactory serviceScopeFactory,
        IOrquestadorContextoServicio orquestadorContextoServicio,
        ILogger<OrquestadorContextoWorker> logger)
    {
        this.colaEventosMensajeriaEntradaServicio = colaEventosMensajeriaEntradaServicio;
        this.serviceScopeFactory = serviceScopeFactory;
        this.orquestadorContextoServicio = orquestadorContextoServicio;
        this.logger = logger;
    }

    public async Task EjecutarAsync(CancellationToken cancellationToken)
    {
        await CargarPendientesConReintentoAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await ProcesarUnEventoAsync(cancellationToken);
        }
    }

    private async Task CargarPendientesConReintentoAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CargarPendientesAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excepcion)
            {
                logger.LogError(excepcion, "Error en carga inicial de eventos pendientes de mensajeria. Se reintentara en {SegundosReintento} segundos.", EsperaReintentoCarga.TotalSeconds);
                await Task.Delay(EsperaReintentoCarga, cancellationToken);
            }
        }
    }

    public async Task CargarPendientesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Inicia carga inicial de eventos pendientes de mensajeria.");

        using IServiceScope alcance = serviceScopeFactory.CreateScope();
        ICargarEventosMensajeriaPendientesAplicacion cargarEventosMensajeriaPendientesAplicacion = alcance.ServiceProvider
            .GetRequiredService<ICargarEventosMensajeriaPendientesAplicacion>();

        List<EventoMensajeriaPendiente> eventosPendientes = await cargarEventosMensajeriaPendientesAplicacion.EjecutarAsync(cancellationToken);

        foreach (EventoMensajeriaPendiente eventoPendiente in eventosPendientes)
        {
            colaEventosMensajeriaEntradaServicio.PublicarRehidratado(ConvertirEvento(eventoPendiente));
        }

        logger.LogInformation("Finaliza carga inicial de eventos pendientes de mensajeria. Eventos={CantidadEventos}", eventosPendientes.Count);
    }

    public async Task ProcesarUnEventoAsync(CancellationToken cancellationToken)
    {
        EventoMensajeriaEntrada eventoMensajeria = await colaEventosMensajeriaEntradaServicio.ConsumirAsync(cancellationToken);
        logger.LogInformation(
            "Evento de mensajeria consumido. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDMensaje={IDMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
            eventoMensajeria.IDProcesamientoInternoMensaje,
            eventoMensajeria.IDMensaje,
            eventoMensajeria.IDConversacion,
            eventoMensajeria.IDLineaConversacion);

        try
        {
            await orquestadorContextoServicio.EncolarAsync(eventoMensajeria, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Error procesando evento de mensajeria. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDMensaje={IDMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                eventoMensajeria.IDProcesamientoInternoMensaje,
                eventoMensajeria.IDMensaje,
                eventoMensajeria.IDConversacion,
                eventoMensajeria.IDLineaConversacion);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EjecutarAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await orquestadorContextoServicio.DisposeAsync();
        }
    }

    private static EventoMensajeriaEntrada ConvertirEvento(EventoMensajeriaPendiente evento)
    {
        return new EventoMensajeriaEntrada
        {
            IDMensaje = evento.IDMensaje,
            IDProcesamientoInternoMensaje = evento.IDProcesamientoInternoMensaje,
            IDConversacion = evento.IDConversacion,
            IDLineaConversacion = evento.IDLineaConversacion,
            FechaCreacion = evento.FechaCreacion
        };
    }
}
