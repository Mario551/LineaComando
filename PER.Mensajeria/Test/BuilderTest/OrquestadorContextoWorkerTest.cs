using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        ServiceProvider serviceProvider = CrearServiceProvider(cargarEventos);
        RegistroLoggerPrueba registroLogger = new();
        OrquestadorContextoWorker worker = new(
            cola,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            orquestador,
            new LoggerPrueba<OrquestadorContextoWorker>(registroLogger));
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(3));

        Task tareaWorker = worker.EjecutarAsync(cancellationTokenSource.Token);
        EventoMensajeria eventoEncolado = await orquestador.EventoEncolado.Task.WaitAsync(cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await EsperarCancelacionAsync(tareaWorker);

        Assert.Equal(cargarEventos.Evento.IDProcesamientoInternoMensaje, eventoEncolado.IDProcesamientoInternoMensaje);
        Assert.Equal(new[] { "carga", "encola" }, pasos);
        registroLogger.AssertSinErrores();
    }

    private static ServiceProvider CrearServiceProvider(
        ICargarEventosMensajeriaPendientesAplicacion cargarEventos)
    {
        ServiceCollection servicios = new();
        servicios.AddScoped(_ => cargarEventos);
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

    private sealed class RegistroLoggerPrueba
    {
        private readonly List<string> errores = [];

        public void Registrar(LogLevel nivel, string mensaje)
        {
            if (nivel >= LogLevel.Error)
            {
                errores.Add(mensaje);
            }
        }

        public void AssertSinErrores()
        {
            Assert.Empty(errores);
        }
    }

    private sealed class LoggerPrueba<T> : ILogger<T>
    {
        private readonly RegistroLoggerPrueba registroLogger;

        public LoggerPrueba(RegistroLoggerPrueba registroLogger)
        {
            this.registroLogger = registroLogger;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return AlcanceLoggerPrueba.Instancia;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            registroLogger.Registrar(logLevel, formatter(state, exception));
        }
    }

    private sealed class AlcanceLoggerPrueba : IDisposable
    {
        public static readonly AlcanceLoggerPrueba Instancia = new();

        public void Dispose()
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

        public TaskCompletionSource<EventoMensajeria> EventoEncolado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EncolarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken)
        {
            pasos.Add("encola");
            EventoEncolado.TrySetResult(eventoMensajeria);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
