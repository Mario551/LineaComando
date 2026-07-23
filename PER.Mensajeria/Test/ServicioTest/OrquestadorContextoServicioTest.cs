using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Servicio.Orquestador;
using ServicioTest.Infraestructura;

namespace ServicioTest;

public class OrquestadorContextoServicioTest
{
    private static readonly TimeSpan TiempoEspera = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task EncolarAsync_MismaConversacion_DebeProcesarEnOrdenFifo()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        ConcurrentQueue<long> orden = new();
        TaskCompletionSource<bool> primerMensajeIniciado = CrearFuente();
        TaskCompletionSource<bool> liberarPrimerMensaje = CrearFuente();
        TaskCompletionSource<bool> segundoMensajeFinalizado = CrearFuente();

        escenario.Control.EjecutarOrquestacionAsync = async (idProcesamiento, cancellationToken) =>
        {
            orden.Enqueue(idProcesamiento);

            if (idProcesamiento == 1)
            {
                primerMensajeIniciado.TrySetResult(true);
                await liberarPrimerMensaje.Task.WaitAsync(cancellationToken);
            }
            else
            {
                segundoMensajeFinalizado.TrySetResult(true);
            }

            return ResultadoOrquestarMensajeContexto.Procesado();
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await primerMensajeIniciado.Task.WaitAsync(TiempoEspera);
        await escenario.Servicio.EncolarAsync(CrearEvento(2, 10), CancellationToken.None);

        Assert.False(segundoMensajeFinalizado.Task.IsCompleted);

        liberarPrimerMensaje.TrySetResult(true);
        await segundoMensajeFinalizado.Task.WaitAsync(TiempoEspera);

        Assert.Equal([1L, 2L], orden.ToArray());
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EncolarAsync_ConversacionesDistintas_DebeProcesarEnParalelo()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        TaskCompletionSource<bool> dosConversacionesActivas = CrearFuente();
        TaskCompletionSource<bool> liberarConversaciones = CrearFuente();
        TaskCompletionSource<bool> dosConversacionesFinalizadas = CrearFuente();
        int conversacionesActivas = 0;
        int maximoConversacionesActivas = 0;
        int conversacionesFinalizadas = 0;

        escenario.Control.EjecutarOrquestacionAsync = async (_, cancellationToken) =>
        {
            int cantidadActiva = Interlocked.Increment(ref conversacionesActivas);
            ActualizarMaximo(ref maximoConversacionesActivas, cantidadActiva);

            if (cantidadActiva == 2)
            {
                dosConversacionesActivas.TrySetResult(true);
            }

            await liberarConversaciones.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref conversacionesActivas);

            if (Interlocked.Increment(ref conversacionesFinalizadas) == 2)
            {
                dosConversacionesFinalizadas.TrySetResult(true);
            }

            return ResultadoOrquestarMensajeContexto.Procesado();
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await escenario.Servicio.EncolarAsync(CrearEvento(2, 20), CancellationToken.None);

        await dosConversacionesActivas.Task.WaitAsync(TiempoEspera);
        Assert.Equal(2, maximoConversacionesActivas);

        liberarConversaciones.TrySetResult(true);
        await dosConversacionesFinalizadas.Task.WaitAsync(TiempoEspera);
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EncolarAsync_SuperaMaximoGlobal_DebeEsperarCupoSinSuperarLimite()
    {
        await using EscenarioOrquestadorPrueba escenario = new(maximoConversacionesConcurrentes: 2);
        TaskCompletionSource<bool> dosConversacionesActivas = CrearFuente();
        TaskCompletionSource<bool> liberarConversaciones = CrearFuente();
        TaskCompletionSource<bool> tresConversacionesFinalizadas = CrearFuente();
        int conversacionesIniciadas = 0;
        int conversacionesActivas = 0;
        int maximoConversacionesActivas = 0;
        int conversacionesFinalizadas = 0;

        escenario.Control.EjecutarOrquestacionAsync = async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref conversacionesIniciadas);
            int cantidadActiva = Interlocked.Increment(ref conversacionesActivas);
            ActualizarMaximo(ref maximoConversacionesActivas, cantidadActiva);

            if (cantidadActiva == 2)
            {
                dosConversacionesActivas.TrySetResult(true);
            }

            await liberarConversaciones.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref conversacionesActivas);

            if (Interlocked.Increment(ref conversacionesFinalizadas) == 3)
            {
                tresConversacionesFinalizadas.TrySetResult(true);
            }

            return ResultadoOrquestarMensajeContexto.Procesado();
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await escenario.Servicio.EncolarAsync(CrearEvento(2, 20), CancellationToken.None);
        await escenario.Servicio.EncolarAsync(CrearEvento(3, 30), CancellationToken.None);

        await dosConversacionesActivas.Task.WaitAsync(TiempoEspera);

        Assert.Equal(2, Volatile.Read(ref conversacionesIniciadas));
        Assert.Equal(2, maximoConversacionesActivas);

        liberarConversaciones.TrySetResult(true);
        await tresConversacionesFinalizadas.Task.WaitAsync(TiempoEspera);

        Assert.Equal(3, conversacionesFinalizadas);
        Assert.Equal(2, maximoConversacionesActivas);
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EncolarAsync_ProcesamientoSigueActivo_DebeRetornarAlAceptarEvento()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        TaskCompletionSource<bool> procesamientoIniciado = CrearFuente();
        TaskCompletionSource<bool> liberarProcesamiento = CrearFuente();
        TaskCompletionSource<bool> procesamientoFinalizado = CrearFuente();

        escenario.Control.EjecutarOrquestacionAsync = async (_, cancellationToken) =>
        {
            procesamientoIniciado.TrySetResult(true);
            await liberarProcesamiento.Task.WaitAsync(cancellationToken);
            procesamientoFinalizado.TrySetResult(true);
            return ResultadoOrquestarMensajeContexto.Procesado();
        };

        Task encolado = escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);

        Assert.True(encolado.IsCompletedSuccessfully);
        await procesamientoIniciado.Task.WaitAsync(TiempoEspera);
        Assert.False(procesamientoFinalizado.Task.IsCompleted);

        liberarProcesamiento.TrySetResult(true);
        await procesamientoFinalizado.Task.WaitAsync(TiempoEspera);
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EncolarAsync_EventoDuplicadoActivoOPendiente_DebeProcesarloUnaSolaVez()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        ConcurrentQueue<long> procesamientos = new();
        TaskCompletionSource<bool> primerMensajeIniciado = CrearFuente();
        TaskCompletionSource<bool> liberarPrimerMensaje = CrearFuente();
        TaskCompletionSource<bool> segundoMensajeFinalizado = CrearFuente();

        escenario.Control.EjecutarOrquestacionAsync = async (idProcesamiento, cancellationToken) =>
        {
            procesamientos.Enqueue(idProcesamiento);

            if (idProcesamiento == 1)
            {
                primerMensajeIniciado.TrySetResult(true);
                await liberarPrimerMensaje.Task.WaitAsync(cancellationToken);
            }
            else
            {
                segundoMensajeFinalizado.TrySetResult(true);
            }

            return ResultadoOrquestarMensajeContexto.Procesado();
        };

        EventoMensajeriaEntrada eventoActivo = CrearEvento(1, 10);
        EventoMensajeriaEntrada eventoPendiente = CrearEvento(2, 10);

        await escenario.Servicio.EncolarAsync(eventoActivo, CancellationToken.None);
        await primerMensajeIniciado.Task.WaitAsync(TiempoEspera);
        await escenario.Servicio.EncolarAsync(eventoActivo, CancellationToken.None);
        await escenario.Servicio.EncolarAsync(eventoPendiente, CancellationToken.None);
        await escenario.Servicio.EncolarAsync(eventoPendiente, CancellationToken.None);

        liberarPrimerMensaje.TrySetResult(true);
        await segundoMensajeFinalizado.Task.WaitAsync(TiempoEspera);

        Assert.Equal([1L, 2L], procesamientos.ToArray());
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EncolarAsync_UnMensajeFalla_DebeContinuarConElSiguiente()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        ConcurrentQueue<long> procesamientos = new();
        TaskCompletionSource<bool> segundoMensajeFinalizado = CrearFuente();

        escenario.Control.EjecutarOrquestacionAsync = (idProcesamiento, _) =>
        {
            procesamientos.Enqueue(idProcesamiento);

            if (idProcesamiento == 1)
            {
                throw new InvalidOperationException("Fallo controlado de A1.");
            }

            segundoMensajeFinalizado.TrySetResult(true);
            return Task.FromResult(ResultadoOrquestarMensajeContexto.Procesado());
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await escenario.Servicio.EncolarAsync(CrearEvento(2, 10), CancellationToken.None);

        await segundoMensajeFinalizado.Task.WaitAsync(TiempoEspera);

        Assert.Equal([1L, 2L], procesamientos.ToArray());
        escenario.RegistroLogger.AssertContieneError("Fallo controlado de A1");
    }

    [Fact]
    public async Task EncolarAsync_A1RequiereRenovacion_DebeReintentarA1AntesDeProcesarA2()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        ConcurrentQueue<string> operaciones = new();
        TaskCompletionSource<bool> primerIntentoA1Iniciado = CrearFuente();
        TaskCompletionSource<bool> permitirRenovacionA1 = CrearFuente();
        TaskCompletionSource<bool> a2Finalizado = CrearFuente();
        int intentosA1 = 0;
        ResultadoCompactacionIntencionContexto compactacion = CrearCompactacion();

        escenario.Control.EjecutarOrquestacionAsync = async (idProcesamiento, cancellationToken) =>
        {
            operaciones.Enqueue($"orquestar:{idProcesamiento}");

            if (idProcesamiento == 1 && Interlocked.Increment(ref intentosA1) == 1)
            {
                primerIntentoA1Iniciado.TrySetResult(true);
                await permitirRenovacionA1.Task.WaitAsync(cancellationToken);
                return ResultadoOrquestarMensajeContexto.RenovarLinea(
                    compactacion,
                    idMensaje: 10,
                    idConversacion: 10,
                    idLineaConversacion: 100);
            }

            if (idProcesamiento == 2)
            {
                a2Finalizado.TrySetResult(true);
            }

            return ResultadoOrquestarMensajeContexto.Procesado();
        };
        escenario.Control.EjecutarRenovacionAsync = (solicitud, _) =>
        {
            operaciones.Enqueue($"renovar:{solicitud.IDProcesamientoInternoMensaje}");
            return Task.FromResult(new ResultadoRenovarLineaContexto
            {
                IDCompactacionContexto = 50,
                IDLineaConversacion = 60,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDConversacion = solicitud.IDConversacion
            });
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await primerIntentoA1Iniciado.Task.WaitAsync(TiempoEspera);
        await escenario.Servicio.EncolarAsync(CrearEvento(2, 10), CancellationToken.None);

        permitirRenovacionA1.TrySetResult(true);
        await a2Finalizado.Task.WaitAsync(TiempoEspera);

        Assert.Equal(
            ["orquestar:1", "renovar:1", "orquestar:1", "orquestar:2"],
            operaciones.ToArray());
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task EncolarAsync_ProcesadorVaciaCola_DebeRetirarloYCrearOtroParaLaConversacion()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        TaskCompletionSource<bool> primerMensajeFinalizado = CrearFuente();
        TaskCompletionSource<bool> segundoMensajeFinalizado = CrearFuente();

        escenario.Control.EjecutarOrquestacionAsync = (idProcesamiento, _) =>
        {
            if (idProcesamiento == 1)
            {
                primerMensajeFinalizado.TrySetResult(true);
            }
            else
            {
                segundoMensajeFinalizado.TrySetResult(true);
            }

            return Task.FromResult(ResultadoOrquestarMensajeContexto.Procesado());
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await primerMensajeFinalizado.Task.WaitAsync(TiempoEspera);
        await EsperarHastaAsync(() => ContarLogs(escenario, "Procesador de conversacion retirado") == 1);

        await escenario.Servicio.EncolarAsync(CrearEvento(2, 10), CancellationToken.None);
        await segundoMensajeFinalizado.Task.WaitAsync(TiempoEspera);
        await EsperarHastaAsync(() => ContarLogs(escenario, "Procesador de conversacion retirado") == 2);
        await EsperarHastaAsync(() => escenario.Control.AlcancesDispuestos == 2);

        Assert.Equal(2, ContarLogs(escenario, "Procesador de conversacion creado"));
        Assert.Equal(2, escenario.Control.AlcancesCreados);
        Assert.Equal(2, escenario.Control.AlcancesDispuestos);
        escenario.RegistroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task DisposeAsync_ProcesamientoActivo_DebeCancelarYEsperarSuFinalizacion()
    {
        await using EscenarioOrquestadorPrueba escenario = new();
        TaskCompletionSource<bool> procesamientoIniciado = CrearFuente();
        TaskCompletionSource<bool> cancelacionObservada = CrearFuente();
        TaskCompletionSource<bool> permitirFinalizacion = CrearFuente();

        escenario.Control.EjecutarOrquestacionAsync = async (_, cancellationToken) =>
        {
            procesamientoIniciado.TrySetResult(true);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelacionObservada.TrySetResult(true);
                await permitirFinalizacion.Task;
                throw;
            }

            return ResultadoOrquestarMensajeContexto.Procesado();
        };

        await escenario.Servicio.EncolarAsync(CrearEvento(1, 10), CancellationToken.None);
        await procesamientoIniciado.Task.WaitAsync(TiempoEspera);

        Task disposicion = escenario.Servicio.DisposeAsync().AsTask();

        await cancelacionObservada.Task.WaitAsync(TiempoEspera);
        Assert.False(disposicion.IsCompleted);

        permitirFinalizacion.TrySetResult(true);
        await disposicion.WaitAsync(TiempoEspera);

        Assert.Equal(1, escenario.Control.AlcancesDispuestos);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            escenario.Servicio.EncolarAsync(CrearEvento(2, 10), CancellationToken.None));
    }

    private static EventoMensajeriaEntrada CrearEvento(long idProcesamiento, long idConversacion)
    {
        return new EventoMensajeriaEntrada
        {
            IDMensaje = idProcesamiento * 10,
            IDProcesamientoInternoMensaje = idProcesamiento,
            IDConversacion = idConversacion,
            IDLineaConversacion = idConversacion * 10,
            FechaCreacion = DateTime.UtcNow
        };
    }

    private static ResultadoCompactacionIntencionContexto CrearCompactacion()
    {
        return ResultadoCompactacionIntencionContexto.Exito(
            "snapshot",
            new InformacionTecnicaLlamadaIAContexto
            {
                Proveedor = "fake",
                Modelo = "fake",
                Adaptador = "fake",
                AccionDecidida = "Compactar"
            });
    }

    private static TaskCompletionSource<bool> CrearFuente()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void ActualizarMaximo(ref int maximoActual, int valor)
    {
        while (true)
        {
            int valorActual = maximoActual;

            if (valor <= valorActual)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref maximoActual, valor, valorActual) == valorActual)
            {
                return;
            }
        }
    }

    private static int ContarLogs(EscenarioOrquestadorPrueba escenario, string texto)
    {
        return escenario.RegistroLogger.Entradas.Count(entrada =>
            entrada.Mensaje.Contains(texto, StringComparison.Ordinal));
    }

    private static async Task EsperarHastaAsync(Func<bool> condicion)
    {
        using CancellationTokenSource espera = new(TiempoEspera);

        while (!condicion())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), espera.Token);
        }
    }

    private sealed class EscenarioOrquestadorPrueba : IAsyncDisposable
    {
        private readonly ServiceProvider proveedorServicios;

        public EscenarioOrquestadorPrueba(int maximoConversacionesConcurrentes = 16)
        {
            Control = new ControlOrquestacionPrueba();
            RegistroLogger = new RegistroLoggerPrueba();
            ServiceCollection servicios = new();
            servicios.AddSingleton(Control);
            servicios.AddScoped<IOrquestarMensajeContextoAplicacion>(proveedor =>
                new OrquestarMensajeContextoAplicacionPrueba(
                    proveedor.GetRequiredService<ControlOrquestacionPrueba>()));
            servicios.AddScoped<IRenovarLineaContextoAplicacion>(proveedor =>
                new RenovarLineaContextoAplicacionPrueba(
                    proveedor.GetRequiredService<ControlOrquestacionPrueba>()));
            proveedorServicios = servicios.BuildServiceProvider();

            Servicio = new OrquestadorContextoServicio(
                proveedorServicios.GetRequiredService<IServiceScopeFactory>(),
                new ConfiguracionOrquestadorContexto
                {
                    MaximoConversacionesConcurrentes = maximoConversacionesConcurrentes
                },
                new LoggerOrquestadorPrueba(RegistroLogger));
        }

        public ControlOrquestacionPrueba Control { get; }
        public RegistroLoggerPrueba RegistroLogger { get; }
        public OrquestadorContextoServicio Servicio { get; }

        public async ValueTask DisposeAsync()
        {
            await Servicio.DisposeAsync();
            await proveedorServicios.DisposeAsync();
        }
    }

    private sealed class ControlOrquestacionPrueba
    {
        private int alcancesCreados;
        private int alcancesDispuestos;

        public Func<long, CancellationToken, Task<ResultadoOrquestarMensajeContexto>> EjecutarOrquestacionAsync { get; set; }
            = (_, _) => Task.FromResult(ResultadoOrquestarMensajeContexto.Procesado());

        public Func<SolicitudRenovarLineaContexto, CancellationToken, Task<ResultadoRenovarLineaContexto>> EjecutarRenovacionAsync { get; set; }
            = (solicitud, _) => Task.FromResult(new ResultadoRenovarLineaContexto
            {
                IDCompactacionContexto = 1,
                IDLineaConversacion = solicitud.IDLineaConversacionOrigen + 1,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDConversacion = solicitud.IDConversacion
            });

        public int AlcancesCreados => Volatile.Read(ref alcancesCreados);
        public int AlcancesDispuestos => Volatile.Read(ref alcancesDispuestos);

        public void RegistrarAlcanceCreado()
        {
            Interlocked.Increment(ref alcancesCreados);
        }

        public void RegistrarAlcanceDispuesto()
        {
            Interlocked.Increment(ref alcancesDispuestos);
        }
    }

    private sealed class OrquestarMensajeContextoAplicacionPrueba : IOrquestarMensajeContextoAplicacion, IAsyncDisposable
    {
        private readonly ControlOrquestacionPrueba control;

        public OrquestarMensajeContextoAplicacionPrueba(ControlOrquestacionPrueba control)
        {
            this.control = control;
            control.RegistrarAlcanceCreado();
        }

        public Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
            long idProcesamientoInternoMensaje,
            CancellationToken cancellationToken)
        {
            return control.EjecutarOrquestacionAsync(idProcesamientoInternoMensaje, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            control.RegistrarAlcanceDispuesto();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RenovarLineaContextoAplicacionPrueba : IRenovarLineaContextoAplicacion
    {
        private readonly ControlOrquestacionPrueba control;

        public RenovarLineaContextoAplicacionPrueba(ControlOrquestacionPrueba control)
        {
            this.control = control;
        }

        public Task<ResultadoRenovarLineaContexto> EjecutarAsync(
            SolicitudRenovarLineaContexto solicitud,
            CancellationToken cancellationToken)
        {
            return control.EjecutarRenovacionAsync(solicitud, cancellationToken);
        }
    }

    private sealed class LoggerOrquestadorPrueba : ILogger<OrquestadorContextoServicio>
    {
        private readonly RegistroLoggerPrueba registro;

        public LoggerOrquestadorPrueba(RegistroLoggerPrueba registro)
        {
            this.registro = registro;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
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
            registro.Registrar(new EntradaLogPrueba(
                logLevel,
                typeof(OrquestadorContextoServicio).FullName ?? nameof(OrquestadorContextoServicio),
                formatter(state, exception),
                exception));
        }
    }
}
