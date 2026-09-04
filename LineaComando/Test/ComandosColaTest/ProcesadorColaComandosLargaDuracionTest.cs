using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Notificaciones;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosColaTest;

public class ProcesadorColaComandosLargaDuracionTest
{
    private static readonly TimeSpan Umbral = TimeSpan.FromMilliseconds(20);

    [Fact]
    public void Constructor_ConConfiguracionLargaDuracionInvalida_DebeLanzarExcepcion()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        IColaComandosMemoria cola = Mock.Of<IColaComandosMemoria>();
        IPublicadorNotificacionEjecucionComandos publicador =
            Mock.Of<IPublicadorNotificacionEjecucionComandos>();
        LoggerPrueba logger = new();

        Assert.Throws<ArgumentException>(() => new ProcesadorColaComandos(
            scopeFactory,
            cola,
            publicador,
            1,
            TimeSpan.Zero,
            1,
            logger));
        Assert.Throws<ArgumentException>(() => new ProcesadorColaComandos(
            scopeFactory,
            cola,
            publicador,
            1,
            Umbral,
            0,
            logger));
    }

    [Fact]
    public async Task StartAsync_ComandoLargo_DebeLiberarElCupoNormal()
    {
        ComandoControlado comandoLargo = new();
        ComandoControlado comandoCorto = new(completarInmediatamente: true);
        using EscenarioProcesador escenario = CrearEscenario(
            1,
            (1, "largo", comandoLargo),
            (2, "corto", comandoCorto));
        Task procesamiento = escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await comandoLargo.EsperarInicioAsync();
            await escenario.Logger.EsperarAsync("El comando 1 continúa como tarea de larga duración");

            await comandoCorto.EsperarInicioAsync();
        }
        finally
        {
            comandoLargo.Completar();
            comandoCorto.Completar();
            await procesamiento.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        Assert.False(escenario.Logger.TieneErrores);
    }

    [Fact]
    public async Task StartAsync_MaximoLargas_DebeLimitarLosComandosQueLiberanCupoNormal()
    {
        ComandoControlado primerLargo = new();
        ComandoControlado segundoLargo = new();
        ComandoControlado tercero = new(completarInmediatamente: true);
        using EscenarioProcesador escenario = CrearEscenario(
            1,
            (1, "primero", primerLargo),
            (2, "segundo", segundoLargo),
            (3, "tercero", tercero));
        Task procesamiento = escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await escenario.Logger.EsperarAsync("El comando 1 continúa como tarea de larga duración");
            await segundoLargo.EsperarInicioAsync();
            await escenario.Logger.EsperarAsync("El comando `2` superó el umbral");
            Assert.False(tercero.Iniciado.Task.IsCompleted);

            primerLargo.Completar();
            await escenario.Logger.EsperarAsync("El comando 2 continúa como tarea de larga duración");
            await tercero.EsperarInicioAsync();
        }
        finally
        {
            primerLargo.Completar();
            segundoLargo.Completar();
            tercero.Completar();
            await procesamiento.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        Assert.False(escenario.Logger.TieneErrores);
    }

    [Fact]
    public async Task StartAsync_ComandoTerminaEsperandoCupoLargo_NoDebeLiberarCupoNoAdquirido()
    {
        ComandoControlado primerLargo = new();
        ComandoControlado comandoEnEspera = new();
        ComandoControlado comandoPosterior = new(completarInmediatamente: true);
        using EscenarioProcesador escenario = CrearEscenario(
            1,
            (1, "primero", primerLargo),
            (2, "espera", comandoEnEspera),
            (3, "posterior", comandoPosterior));
        Task procesamiento = escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await escenario.Logger.EsperarAsync("El comando 1 continúa como tarea de larga duración");
            await comandoEnEspera.EsperarInicioAsync();
            await escenario.Logger.EsperarAsync("El comando `2` superó el umbral");

            comandoEnEspera.Completar();
            await escenario.EsperarPersistenciaAsync(2);
            await comandoPosterior.EsperarInicioAsync();
            await escenario.EsperarPersistenciaAsync(3);

            primerLargo.Completar();
        }
        finally
        {
            primerLargo.Completar();
            comandoEnEspera.Completar();
            comandoPosterior.Completar();
            await procesamiento.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        Assert.False(escenario.Logger.TieneErrores);
    }

    private static EscenarioProcesador CrearEscenario(
        int maxTareasLargaDuracion,
        params (long Id, string RutaLocal, ComandoBase<string, ResultadoComando> Comando)[] definiciones)
    {
        Dictionary<long, ComandoEnCola> comandos = definiciones.ToDictionary(
            definicion => definicion.Id,
            definicion => new ComandoEnCola
            {
                Id = definicion.Id,
                RutaComando = $"test {definicion.RutaLocal}",
                FechaCreacion = DateTime.UtcNow,
                Estado = "pendiente"
            });
        ConcurrentDictionary<long, TaskCompletionSource<bool>> persistencias = new();
        Mock<IAlmacenColaComandos> almacen = new();
        almacen
            .Setup(a => a.MarcarComandosProcesandoAsync(
                It.IsAny<long[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((long[] ids, CancellationToken _) =>
                ids.Select(id => comandos[id]));
        almacen
            .Setup(a => a.MarcarComoProcesadoAsync(
                It.IsAny<long>(),
                It.IsAny<ResultadoComando>(),
                It.IsAny<PayloadResultadoComando?>(),
                It.IsAny<CancellationToken>()))
            .Returns((long id, ResultadoComando _, PayloadResultadoComando? _, CancellationToken _) =>
            {
                ObtenerSenal(persistencias, id).TrySetResult(true);
                return Task.CompletedTask;
            });

        FactoriaComandos<string, ResultadoComando> factoriaComandos = new("test");
        foreach (var definicion in definiciones)
        {
            factoriaComandos.Add(
                definicion.RutaLocal,
                new Nodo<string, ResultadoComando>(definicion.Comando));
        }

        FactoriaAbstractaComandos<string, ResultadoComando> factoria = new([factoriaComandos]);
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(almacen.Object)
            .AddSingleton<IFactoriaAbstractaComandos<string, ResultadoComando>>(factoria)
            .BuildServiceProvider();
        Mock<IColaComandosMemoria> cola = new();
        cola
            .Setup(c => c.CargarPendientesDesdeBaseDatosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cola
            .Setup(c => c.LeerAsync(It.IsAny<CancellationToken>()))
            .Returns(LeerComandosAsync(comandos.Values.OrderBy(comando => comando.Id)));
        LoggerPrueba logger = new();
        ProcesadorColaComandos procesador = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            cola.Object,
            Mock.Of<IPublicadorNotificacionEjecucionComandos>(),
            1,
            Umbral,
            maxTareasLargaDuracion,
            logger);
        return new EscenarioProcesador(procesador, logger, persistencias, provider);
    }

    private static TaskCompletionSource<bool> ObtenerSenal(
        ConcurrentDictionary<long, TaskCompletionSource<bool>> senales,
        long id)
    {
        return senales.GetOrAdd(
            id,
            _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private static async IAsyncEnumerable<ComandoEnCola> LeerComandosAsync(
        IEnumerable<ComandoEnCola> comandos)
    {
        foreach (ComandoEnCola comando in comandos)
        {
            await Task.Yield();
            yield return comando;
        }
    }

    private sealed class ComandoControlado : ComandoBase<string, ResultadoComando>
    {
        private readonly TaskCompletionSource<bool> _completar =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _completarInmediatamente;

        public ComandoControlado(bool completarInmediatamente = false)
        {
            _completarInmediatamente = completarInmediatamente;
        }

        public TaskCompletionSource<bool> Iniciado { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Preparar(ICollection<Parametro> parametros)
        {
        }

        public override async Task<ResultadoComando> EjecutarAsync(
            string entrada,
            CancellationToken token = default)
        {
            Iniciado.TrySetResult(true);

            if (!_completarInmediatamente)
                await _completar.Task.WaitAsync(token);

            return ResultadoComando.Exito();
        }

        public Task EsperarInicioAsync()
        {
            return Iniciado.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        public void Completar()
        {
            _completar.TrySetResult(true);
        }
    }

    private sealed class EscenarioProcesador : IDisposable
    {
        private readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _persistencias;
        private readonly ServiceProvider _provider;

        public EscenarioProcesador(
            ProcesadorColaComandos procesador,
            LoggerPrueba logger,
            ConcurrentDictionary<long, TaskCompletionSource<bool>> persistencias,
            ServiceProvider provider)
        {
            Procesador = procesador;
            Logger = logger;
            _persistencias = persistencias;
            _provider = provider;
        }

        public ProcesadorColaComandos Procesador { get; }

        public LoggerPrueba Logger { get; }

        public Task EsperarPersistenciaAsync(long id)
        {
            return ObtenerSenal(_persistencias, id).Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }

    private sealed class LoggerPrueba : ILogger<ProcesadorColaComandos>
    {
        private readonly ConcurrentQueue<(LogLevel Nivel, string Mensaje, Exception? Excepcion)> _registros = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _esperas = new();

        public bool TieneErrores => _registros.Any(registro => registro.Nivel >= LogLevel.Error);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
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
            string mensaje = formatter(state, exception);
            _registros.Enqueue((logLevel, mensaje, exception));

            foreach (KeyValuePair<string, TaskCompletionSource<bool>> espera in _esperas)
            {
                if (mensaje.Contains(espera.Key, StringComparison.Ordinal))
                    espera.Value.TrySetResult(true);
            }
        }

        public Task EsperarAsync(string contenido)
        {
            TaskCompletionSource<bool> espera = _esperas.GetOrAdd(
                contenido,
                _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

            if (_registros.Any(registro => registro.Mensaje.Contains(contenido, StringComparison.Ordinal)))
                espera.TrySetResult(true);

            return espera.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }
    }
}
