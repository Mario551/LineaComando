using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Builder.Worker;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Orquestador;

namespace BuilderTest;

public class OrquestadorContextoWorkerTest
{
    [Fact]
    public async Task EjecutarAsync_DebeCargarPendientesAntesDeProcesarCola()
    {
        ColaEventosMensajeriaServicio cola = new();
        List<string> pasos = new();
        FakeCargarEventosMensajeriaPendientesAplicacion cargarEventos = new(pasos);
        FakeOrquestadorContextoServicio orquestador = new(pasos);
        ServiceProvider serviceProvider = CrearServiceProvider(cargarEventos, orquestador);
        OrquestadorContextoWorker worker = new(
            cola,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(3));

        Task tareaWorker = worker.EjecutarAsync(cancellationTokenSource.Token);
        EventoMensajeria eventoProcesado = await orquestador.EventoProcesado.Task.WaitAsync(cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await EsperarCancelacionAsync(tareaWorker);

        Assert.Equal(cargarEventos.Evento.IDProcesamientoInternoMensaje, eventoProcesado.IDProcesamientoInternoMensaje);
        Assert.Equal(new[] { "carga", "procesa" }, pasos);
    }

    private static ServiceProvider CrearServiceProvider(
        ICargarEventosMensajeriaPendientesAplicacion cargarEventos,
        IOrquestadorContextoServicio orquestador)
    {
        ServiceCollection servicios = new();
        servicios.AddScoped(_ => cargarEventos);
        servicios.AddScoped(_ => orquestador);
        return servicios.BuildServiceProvider();
    }

    private static async Task EsperarCancelacionAsync(Task tarea)
    {
        try
        {
            await tarea;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class FakeCargarEventosMensajeriaPendientesAplicacion : ICargarEventosMensajeriaPendientesAplicacion
    {
        private readonly List<string> pasos;

        public FakeCargarEventosMensajeriaPendientesAplicacion(List<string> pasos)
        {
            this.pasos = pasos;
        }

        public EventoMensajeriaPendiente Evento { get; } = new()
        {
            IDMensaje = 10,
            IDProcesamientoInternoMensaje = 20,
            IDConversacion = 30,
            IDLineaConversacion = 40,
            FechaCreacion = DateTime.Now
        };

        public Task<List<EventoMensajeriaPendiente>> EjecutarAsync(CancellationToken cancellationToken)
        {
            pasos.Add("carga");
            return Task.FromResult(new List<EventoMensajeriaPendiente> { Evento });
        }
    }

    private sealed class FakeOrquestadorContextoServicio : IOrquestadorContextoServicio
    {
        private readonly List<string> pasos;

        public FakeOrquestadorContextoServicio(List<string> pasos)
        {
            this.pasos = pasos;
        }

        public TaskCompletionSource<EventoMensajeria> EventoProcesado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ProcesarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken)
        {
            pasos.Add("procesa");
            EventoProcesado.TrySetResult(eventoMensajeria);
            return Task.CompletedTask;
        }
    }
}
