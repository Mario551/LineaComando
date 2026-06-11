using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Orquestador;

namespace PER.Mensajeria.Builder.Worker;

public class OrquestadorContextoWorker : BackgroundService
{
    private static readonly TimeSpan EsperaReintentoCarga = TimeSpan.FromSeconds(5);

    private readonly IColaEventosMensajeriaServicio colaEventosMensajeriaServicio;
    private readonly IServiceScopeFactory serviceScopeFactory;

    public OrquestadorContextoWorker(
        IColaEventosMensajeriaServicio colaEventosMensajeriaServicio,
        IServiceScopeFactory serviceScopeFactory)
    {
        this.colaEventosMensajeriaServicio = colaEventosMensajeriaServicio;
        this.serviceScopeFactory = serviceScopeFactory;
    }

    public async Task EjecutarAsync(CancellationToken cancellationToken)
    {
        await CargarPendientesConReintentoAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await ProcesarUnEventoAsync(cancellationToken);
        }
    }

    public async Task CargarPendientesAsync(CancellationToken cancellationToken)
    {
        using IServiceScope alcance = serviceScopeFactory.CreateScope();
        ICargarEventosMensajeriaPendientesAplicacion cargarEventosMensajeriaPendientesAplicacion = alcance.ServiceProvider
            .GetRequiredService<ICargarEventosMensajeriaPendientesAplicacion>();

        List<EventoMensajeriaPendiente> eventosPendientes = await cargarEventosMensajeriaPendientesAplicacion.EjecutarAsync(cancellationToken);

        foreach (EventoMensajeriaPendiente eventoPendiente in eventosPendientes)
        {
            colaEventosMensajeriaServicio.Publicar(ConvertirEvento(eventoPendiente));
        }
    }

    public async Task ProcesarUnEventoAsync(CancellationToken cancellationToken)
    {
        EventoMensajeria eventoMensajeria = await colaEventosMensajeriaServicio.ConsumirAsync(cancellationToken);

        using IServiceScope alcance = serviceScopeFactory.CreateScope();
        IOrquestadorContextoServicio orquestadorContextoServicio = alcance.ServiceProvider
            .GetRequiredService<IOrquestadorContextoServicio>();

        await orquestadorContextoServicio.ProcesarAsync(eventoMensajeria, cancellationToken);
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
            catch
            {
                await Task.Delay(EsperaReintentoCarga, cancellationToken);
            }
        }
    }

    private static EventoMensajeria ConvertirEvento(EventoMensajeriaPendiente evento)
    {
        return new EventoMensajeria
        {
            IDMensaje = evento.IDMensaje,
            IDProcesamientoInternoMensaje = evento.IDProcesamientoInternoMensaje,
            IDConversacion = evento.IDConversacion,
            IDLineaConversacion = evento.IDLineaConversacion,
            FechaCreacion = evento.FechaCreacion
        };
    }
}
