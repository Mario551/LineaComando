using BuilderTest.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Builder.Worker;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Mensaje;

namespace BuilderTest;

public class MensajeriaWorkerTest
{
    [Fact]
    public async Task ExecuteAsync_DebeProcesarEntradaYSalidaEnCiclosConcurrentes()
    {
        DTORegistrarMensajeEntranteSolicitud entrada = CrearEntrada();
        DTOEnvioMensajePendiente salida = CrearSalida(21);
        ComunicacionMensajeriaPrueba comunicacion = new(entrada);
        MensajeServicioPrueba mensajeServicio = new(salida);
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        CargarEventosSalidaPrueba cargarEventos = new([]);
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(cargarEventos);
        MensajeriaWorker worker = CrearWorker(
            comunicacion,
            mensajeServicio,
            colaSalida,
            proveedor,
            registroLogger);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        await worker.StartAsync(CancellationToken.None);
        DTORegistrarMensajeEntranteSolicitud entradaRecibida =
            await mensajeServicio.EntradaRecibida.Task.WaitAsync(timeout.Token);
        DTOResultadoEnvioMensaje resultado =
            await mensajeServicio.ResultadoRegistrado.Task.WaitAsync(timeout.Token);
        await worker.StopAsync(timeout.Token);

        Assert.Same(entrada, entradaRecibida);
        Assert.Equal(salida.IDEnvioMensaje, resultado.IDEnvioMensaje);
        Assert.Equal("enviado", resultado.Estado);
        Assert.Equal(1, comunicacion.CantidadEnvios);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task ExecuteAsync_CargaSalidasBloqueada_DebeContinuarRecibiendoEntradas()
    {
        DTORegistrarMensajeEntranteSolicitud entrada = CrearEntrada();
        ComunicacionMensajeriaPrueba comunicacion = new(entrada);
        MensajeServicioPrueba mensajeServicio = new(CrearSalida(22));
        CargarEventosSalidaBloqueadoPrueba cargarEventos = new();
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(cargarEventos);
        MensajeriaWorker worker = CrearWorker(
            comunicacion,
            mensajeServicio,
            new ColaEventosMensajeriaSalidaServicio(),
            proveedor,
            registroLogger);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        await worker.StartAsync(CancellationToken.None);
        await cargarEventos.Iniciado.Task.WaitAsync(timeout.Token);
        DTORegistrarMensajeEntranteSolicitud entradaRecibida =
            await mensajeServicio.EntradaRecibida.Task.WaitAsync(timeout.Token);
        await worker.StopAsync(timeout.Token);

        Assert.Same(entrada, entradaRecibida);
        Assert.False(mensajeServicio.ResultadoRegistrado.Task.IsCompleted);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task CargarSalidasPendientesAsync_DebePublicarEventosRehidratados()
    {
        EventoMensajeriaSalida evento = new()
        {
            IDEnvioMensaje = 33,
            FechaCreacion = DateTime.Now
        };
        CargarEventosSalidaPrueba cargarEventos = new([evento]);
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(cargarEventos);
        MensajeriaWorker worker = CrearWorker(
            new ComunicacionMensajeriaPrueba(CrearEntrada()),
            new MensajeServicioPrueba(CrearSalida(33)),
            colaSalida,
            proveedor,
            registroLogger);

        await worker.CargarSalidasPendientesAsync(CancellationToken.None);
        EventoMensajeriaSalida consumido = await colaSalida.ConsumirAsync(CancellationToken.None);

        Assert.Same(evento, consumido);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task ProcesarSalidaAsync_ProveedorFalla_DebeRegistrarResultadoFallido()
    {
        DTOEnvioMensajePendiente salida = CrearSalida(44);
        ComunicacionMensajeriaPrueba comunicacion = new(CrearEntrada())
        {
            ExcepcionEnvio = new InvalidOperationException("fallo proveedor")
        };
        MensajeServicioPrueba mensajeServicio = new(salida);
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(new CargarEventosSalidaPrueba([]));
        MensajeriaWorker worker = CrearWorker(
            comunicacion,
            mensajeServicio,
            new ColaEventosMensajeriaSalidaServicio(),
            proveedor,
            registroLogger);

        await worker.ProcesarSalidaAsync(salida, CancellationToken.None);

        DTOResultadoEnvioMensaje resultado =
            await mensajeServicio.ResultadoRegistrado.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(salida.IDEnvioMensaje, resultado.IDEnvioMensaje);
        Assert.Equal("fallido", resultado.Estado);
        Assert.Contains("fallo proveedor", resultado.Error);
        Assert.Contains(
            registroLogger.Entradas,
            entrada => entrada.Nivel == LogLevel.Error
                && entrada.Mensaje.Contains("Error enviando mensaje", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcesarSalidaAsync_Cancelado_DebePropagarYNoRegistrarResultado()
    {
        DTOEnvioMensajePendiente salida = CrearSalida(55);
        ComunicacionMensajeriaPrueba comunicacion = new(CrearEntrada());
        MensajeServicioPrueba mensajeServicio = new(salida);
        RegistroLoggerPrueba registroLogger = new();
        using ServiceProvider proveedor = CrearProveedor(new CargarEventosSalidaPrueba([]));
        MensajeriaWorker worker = CrearWorker(
            comunicacion,
            mensajeServicio,
            new ColaEventosMensajeriaSalidaServicio(),
            proveedor,
            registroLogger);
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            worker.ProcesarSalidaAsync(salida, cancellationTokenSource.Token));

        Assert.False(mensajeServicio.ResultadoRegistrado.Task.IsCompleted);
        registroLogger.AssertSinErrores();
    }

    private static MensajeriaWorker CrearWorker(
        IComunicacionMensajeriaAPI comunicacion,
        IMensajeServicio mensajeServicio,
        IColaEventosMensajeriaSalidaServicio colaSalida,
        ServiceProvider proveedor,
        RegistroLoggerPrueba registroLogger)
    {
        LoggerFactory loggerFactory = new([
            new LoggerProviderPrueba(registroLogger)
        ]);

        return new MensajeriaWorker(
            comunicacion,
            mensajeServicio,
            colaSalida,
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            loggerFactory.CreateLogger<MensajeriaWorker>());
    }

    private static ServiceProvider CrearProveedor(
        ICargarEventosMensajeriaSalidaPendientesAplicacion cargarEventos)
    {
        ServiceCollection servicios = new();
        servicios.AddScoped(_ => cargarEventos);
        return servicios.BuildServiceProvider();
    }

    private static DTORegistrarMensajeEntranteSolicitud CrearEntrada()
    {
        return new DTORegistrarMensajeEntranteSolicitud
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = "whatsapp",
                Cuenta = "cuenta",
                IdentificadorParticipante = "3001234567",
                TipoParticipante = "telefono",
                TipoMensaje = "texto",
                IdentificadorExternoMensaje = Guid.NewGuid().ToString("N"),
                FechaMensaje = DateTime.Now
            }
        };
    }

    private static DTOEnvioMensajePendiente CrearSalida(long idEnvioMensaje)
    {
        return new DTOEnvioMensajePendiente
        {
            IDEnvioMensaje = idEnvioMensaje,
            Canal = "whatsapp",
            Cuenta = "cuenta",
            Mensaje = new DTOMensajeSaliente
            {
                IDConversacion = 1,
                IDLineaConversacion = 2,
                TipoMensaje = "texto",
                Contenido = "respuesta",
                FechaMensaje = DateTime.Now
            }
        };
    }

    private sealed class ComunicacionMensajeriaPrueba : IComunicacionMensajeriaAPI
    {
        private readonly DTORegistrarMensajeEntranteSolicitud entrada;
        private int entradasEntregadas;

        public ComunicacionMensajeriaPrueba(DTORegistrarMensajeEntranteSolicitud entrada)
        {
            this.entrada = entrada;
        }

        public Exception? ExcepcionEnvio { get; set; }
        public int CantidadEnvios { get; private set; }

        public async Task<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref entradasEntregadas) == 1)
            {
                return entrada;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("La espera debio cancelarse.");
        }

        public Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
            DTOEnvioMensajePendiente mensaje,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CantidadEnvios++;

            if (ExcepcionEnvio is not null)
            {
                throw ExcepcionEnvio;
            }

            return Task.FromResult(new DTOResultadoEnvioMensaje
            {
                IDEnvioMensaje = mensaje.IDEnvioMensaje,
                Estado = "enviado"
            });
        }
    }

    private sealed class MensajeServicioPrueba : IMensajeServicio
    {
        private readonly DTOEnvioMensajePendiente salida;
        private int salidasEntregadas;

        public MensajeServicioPrueba(DTOEnvioMensajePendiente salida)
        {
            this.salida = salida;
        }

        public TaskCompletionSource<DTORegistrarMensajeEntranteSolicitud> EntradaRecibida { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DTOResultadoEnvioMensaje> ResultadoRegistrado { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<DTORegistrarMensajeEntranteRespuesta> RecibirAsync(
            DTORegistrarMensajeEntranteSolicitud solicitud,
            CancellationToken cancellationToken)
        {
            EntradaRecibida.TrySetResult(solicitud);
            return Task.FromResult(new DTORegistrarMensajeEntranteRespuesta
            {
                IDMensaje = 1,
                IDConversacion = 2,
                IDLineaConversacion = 3,
                IDProcesamientoInternoMensaje = 4,
                Registrado = true
            });
        }

        public Task<ResultadoRenovarLineaContexto> RenovarLineaContextoAsync(
            SolicitudRenovarLineaContexto solicitud,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<DTOEnvioMensajePendiente> EsperarMensajeSalidaAsync(
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref salidasEntregadas) == 1)
            {
                return salida;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("La espera debio cancelarse.");
        }

        public Task RegistrarResultadoEnvioAsync(
            DTOResultadoEnvioMensaje resultado,
            CancellationToken cancellationToken)
        {
            ResultadoRegistrado.TrySetResult(resultado);
            return Task.CompletedTask;
        }
    }

    private sealed class CargarEventosSalidaPrueba
        : ICargarEventosMensajeriaSalidaPendientesAplicacion
    {
        private readonly List<EventoMensajeriaSalida> eventos;

        public CargarEventosSalidaPrueba(List<EventoMensajeriaSalida> eventos)
        {
            this.eventos = eventos;
        }

        public Task<List<EventoMensajeriaSalida>> EjecutarAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(eventos);
        }
    }

    private sealed class CargarEventosSalidaBloqueadoPrueba
        : ICargarEventosMensajeriaSalidaPendientesAplicacion
    {
        public TaskCompletionSource Iniciado { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<List<EventoMensajeriaSalida>> EjecutarAsync(
            CancellationToken cancellationToken)
        {
            Iniciado.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("La carga debio cancelarse.");
        }
    }
}
