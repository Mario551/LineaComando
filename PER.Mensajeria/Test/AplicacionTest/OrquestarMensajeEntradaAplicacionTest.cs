using AplicacionTest.Fakes;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;
using LoggerOrquestarMensajeEntrada = AplicacionTest.Infraestructura.LoggerPrueba<PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada.OrquestarMensajeEntradaAplicacion>;
using RegistroLoggerPrueba = AplicacionTest.Infraestructura.RegistroLoggerPrueba;

namespace AplicacionTest;

public class OrquestarMensajeEntradaAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoDevuelveSalidas_DebeRegistrarSalidaEnvioYMarcarProcesado(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        DTOMensajeSaliente mensajeSaliente = await CrearMensajeSalienteDesdeEntradaAsync(baseDatos, mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConSalidas(mensajeSaliente);
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.Procesado, resultado.Tipo);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.True(await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida") > 0);
        Assert.True(await contexto.EnviosMensaje.CountAsync(envioActual => envioActual.IDEstadoEnvioMensaje == "pendiente") > 0);
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoDevuelveSinSalidas_DebeNoRegistrarSalidaNiEnvioYMarcarProcesado(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.SinSalidas, resultado.Tipo);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoDevuelveError_DebeNoCrearSalidaYMarcarProcesamientoError(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConError("Error final del contexto.");
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.Error, resultado.Tipo);
        Assert.Equal("error", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(1, procesamientoActualizado.Intentos);
        Assert.Contains("Error final del contexto", procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
        registroLogger.AssertContieneError("Error final del contexto");
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoLanzaExcepcion_DebeNoCrearSalidaYMarcarProcesamientoError(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConExcepcion(new InvalidOperationException("Fallo contexto fake."));
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.Error, resultado.Tipo);
        Assert.Equal("error", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(1, procesamientoActualizado.Intentos);
        Assert.Contains("Fallo contexto fake", procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
        registroLogger.AssertContieneError("Fallo contexto fake");
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_DebeEnviarSolicitudContextoConIDsYDatosCargadosDesdeBD(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        SolicitudContextoConversacion solicitud = Assert.IsType<SolicitudContextoConversacion>(contextoConversacion.SolicitudRecibida);
        Assert.Equal(procesamiento.ID, solicitud.IDProcesamientoInternoMensaje);
        Assert.Equal(mensaje.ID, solicitud.IDMensaje);
        Assert.Equal(mensaje.IDLineaConversacion, solicitud.IDLineaConversacion);
        Assert.Equal(mensaje.IDTipoMensaje, solicitud.TipoMensaje);
        Assert.Equal(mensaje.TelefonoOrigen, solicitud.TelefonoOrigen);
        Assert.Equal(mensaje.TelefonoDestino, solicitud.TelefonoDestino);
        Assert.Equal(mensaje.Contenido, solicitud.Contenido);
        Assert.Equal(mensaje.IdentificadorExternoMensaje, solicitud.IdentificadorExternoMensaje);
        Assert.Equal(mensaje.FechaMensaje, solicitud.FechaMensaje);
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_LimiteVentana_DebeRetornarRenovarLineaSinCerrarProcesamiento(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        ResultadoCompactacionIntencionContexto compactacion = ResultadoCompactacionIntencionContexto.Exito(
            "snapshot",
            new MetadataRazonamientoIAContexto
            {
                Proveedor = "fake",
                Modelo = "fake",
                Adaptador = "fake",
                AccionDecidida = "Compactar"
            });
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.LimiteVentana(compactacion);
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(
            procesamiento.ID,
            CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();
        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.RenovarLinea, resultado.Tipo);
        Assert.Same(compactacion, resultado.Compactacion);
        Assert.Equal("en_proceso", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Null(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Empty(await contexto.EnviosMensaje.ToListAsync());
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoSimulaComandoIntermedio_DebeProcesarSoloResultadoFinal(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        DTOMensajeSaliente mensajeSaliente = await CrearMensajeSalienteDesdeEntradaAsync(baseDatos, mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConComandoIntermedio(mensajeSaliente);
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        Assert.Equal(1, contextoConversacion.PasosInternosSimulados);
        Assert.Equal(1, await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida"));
        Assert.Equal(1, await contexto.EnviosMensaje.CountAsync());
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoSimulaHistorialIntermedio_DebeProcesarSoloResultadoFinal(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        DTOMensajeSaliente mensajeSaliente = await CrearMensajeSalienteDesdeEntradaAsync(baseDatos, mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConHistorialIntermedio(mensajeSaliente);
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        Assert.Equal(1, contextoConversacion.PasosInternosSimulados);
        Assert.Equal(1, await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida"));
        Assert.Equal(1, await contexto.EnviosMensaje.CountAsync());
        registroLogger.AssertSinErrores();
    }

    private static IOrquestarMensajeEntradaAplicacion CrearAplicacion(
        BaseDatosPrueba baseDatos,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);
        RegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion = new(unitOfWork);

        ILogger<OrquestarMensajeEntradaAplicacion> logger = new LoggerOrquestarMensajeEntrada(registroLogger);

        return new OrquestarMensajeEntradaAplicacion(
            unitOfWork,
            contextoConversacionServicio,
            registrarMensajeSalidaAplicacion,
            logger);
    }

    private static async Task<DTOMensajeSaliente> CrearMensajeSalienteDesdeEntradaAsync(
        BaseDatosPrueba baseDatos,
        DAOMensaje mensajeEntrada)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOLineaConversacion linea = await contexto.LineasConversacion.SingleAsync(
            lineaActual => lineaActual.ID == mensajeEntrada.IDLineaConversacion);

        return new DTOMensajeSaliente
        {
            IDConversacion = linea.IDConversacion,
            IDLineaConversacion = mensajeEntrada.IDLineaConversacion,
            TipoMensaje = mensajeEntrada.IDTipoMensaje,
            TelefonoOrigen = mensajeEntrada.TelefonoDestino,
            TelefonoDestino = mensajeEntrada.TelefonoOrigen,
            Contenido = "respuesta contexto",
            FechaMensaje = DateTime.Now
        };
    }
}
