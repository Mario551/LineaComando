using AplicacionTest.Fakes;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
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
        MensajeSalienteContexto mensajeSaliente = CrearMensajeSalienteDesdeEntrada(mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConSalidas(mensajeSaliente);
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();
        DAOMensaje mensajeSalida = await contexto.Mensajes.SingleAsync(
            mensajeActual => mensajeActual.IDDireccionMensaje == "salida");

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.Procesado, resultado.Tipo);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.Equal(mensaje.IDLineaConversacion, mensajeSalida.IDLineaConversacion);
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
    public async Task EjecutarAsync_HostCancela_DebeConservarProcesamientoEnProcesoYPropagarCancelacion(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        using CancellationTokenSource fuenteCancelacion = new();
        FakeContextoConversacionServicio contextoConversacion =
            FakeContextoConversacionServicio.ConCancelacion(fuenteCancelacion);
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            aplicacion.EjecutarAsync(procesamiento.ID, fuenteCancelacion.Token));

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje
            .SingleAsync(procesamientoActual => procesamientoActual.ID == procesamiento.ID);

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal("en_proceso", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(0, procesamientoActualizado.Intentos);
        Assert.Null(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        registroLogger.AssertSinErrores();
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
            new InformacionTecnicaLlamadaIAContexto
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
        Assert.Equal(mensaje.ID, resultado.IDMensaje);
        Assert.Equal(mensaje.IDLineaConversacion, resultado.IDLineaConversacion);
        Assert.Equal("en_proceso", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Null(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Empty(await contexto.EnviosMensaje.ToListAsync());
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_MensajeEnLineaInactiva_DebeRealinearloALineaActiva(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        long idLineaActiva;

        await using (MensajeriaContextoDB contextoPreparacion = baseDatos.CrearContexto())
        {
            DAOLineaConversacion lineaAnterior = await contextoPreparacion.LineasConversacion
                .SingleAsync(linea => linea.ID == mensaje.IDLineaConversacion);
            lineaAnterior.Activa = false;
            DAOLineaConversacion lineaActiva = new()
            {
                IDConversacion = lineaAnterior.IDConversacion,
                FechaInicio = DateTime.Now,
                FechaUltimaActividad = DateTime.Now,
                Activa = true
            };
            await contextoPreparacion.LineasConversacion.AddAsync(lineaActiva);
            await contextoPreparacion.SaveChangesAsync();
            idLineaActiva = lineaActiva.ID;
        }

        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        SolicitudContextoConversacion solicitud = Assert.IsType<SolicitudContextoConversacion>(
            contextoConversacion.SolicitudRecibida);
        await using MensajeriaContextoDB contextoVerificacion = baseDatos.CrearContexto();
        long idLineaMensaje = await contextoVerificacion.Mensajes
            .Where(mensajeActual => mensajeActual.ID == mensaje.ID)
            .Select(mensajeActual => mensajeActual.IDLineaConversacion)
            .SingleAsync();

        Assert.Equal(idLineaActiva, solicitud.IDLineaConversacion);
        Assert.Equal(idLineaActiva, idLineaMensaje);
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ProcesamientoTerminal_DebeIgnorarRedelivery(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje _, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();

        await using (MensajeriaContextoDB contextoPreparacion = baseDatos.CrearContexto())
        {
            DAOProcesamientoInternoMensaje procesamientoTerminal = await contextoPreparacion.ProcesamientosInternosMensaje
                .SingleAsync(procesamientoActual => procesamientoActual.ID == procesamiento.ID);
            procesamientoTerminal.IDEstadoProcesamientoInternoMensaje = "procesado";
            procesamientoTerminal.FechaProcesado = DateTime.Now;
            await contextoPreparacion.SaveChangesAsync();
        }

        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger);

        ResultadoOrquestarMensajeEntrada resultado = await aplicacion.EjecutarAsync(
            procesamiento.ID,
            CancellationToken.None);

        Assert.Equal(ResultadoOrquestarMensajeEntradaTipo.Procesado, resultado.Tipo);
        Assert.False(contextoConversacion.Ejecutado);
        await using MensajeriaContextoDB contextoVerificacion = baseDatos.CrearContexto();
        Assert.Equal(1, await contextoVerificacion.Mensajes.CountAsync());
        Assert.Empty(await contextoVerificacion.EnviosMensaje.ToListAsync());
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoSimulaComandoIntermedio_DebeProcesarSoloResultadoFinal(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        MensajeSalienteContexto mensajeSaliente = CrearMensajeSalienteDesdeEntrada(mensaje);
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
    public async Task EjecutarAsync_ContextoSimulaConsultaMensajesAnteriores_DebeProcesarSoloResultadoFinal(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        MensajeSalienteContexto mensajeSaliente = CrearMensajeSalienteDesdeEntrada(mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConConsultaMensajesAnterioresIntermedia(mensajeSaliente);
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
    public async Task EjecutarAsync_DebeDisponerUnitOfWorkAntesDeResolverContexto(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje _, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        contextoConversacion.AntesDeResolver = () =>
        {
            Assert.True(unitOfWorkFactory.AlcancesCreados > 0);
            Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
            Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
        };
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(
            unitOfWorkFactory,
            contextoConversacion,
            registroLogger);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
        Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
        registroLogger.AssertSinErrores();
    }

    private static IOrquestarMensajeEntradaAplicacion CrearAplicacion(
        BaseDatosPrueba baseDatos,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger)
    {
        return CrearAplicacion(
            new UnitOfWorkFactoryPrueba(baseDatos),
            contextoConversacionServicio,
            registroLogger);
    }

    private static IOrquestarMensajeEntradaAplicacion CrearAplicacion(
        UnitOfWorkFactoryPrueba unitOfWorkFactory,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger)
    {
        RegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion = new(unitOfWorkFactory);

        ILogger<OrquestarMensajeEntradaAplicacion> logger = new LoggerOrquestarMensajeEntrada(registroLogger);

        return new OrquestarMensajeEntradaAplicacion(
            unitOfWorkFactory,
            contextoConversacionServicio,
            registrarMensajeSalidaAplicacion,
            logger);
    }

    private static MensajeSalienteContexto CrearMensajeSalienteDesdeEntrada(DAOMensaje mensajeEntrada)
    {
        return new MensajeSalienteContexto
        {
            TipoMensaje = mensajeEntrada.IDTipoMensaje,
            TelefonoOrigen = mensajeEntrada.TelefonoDestino,
            TelefonoDestino = mensajeEntrada.TelefonoOrigen,
            Contenido = "respuesta contexto",
            FechaMensaje = DateTime.Now
        };
    }
}
