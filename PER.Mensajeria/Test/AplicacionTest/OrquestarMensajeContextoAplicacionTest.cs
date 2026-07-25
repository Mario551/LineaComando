using AplicacionTest.Fakes;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
using LoggerOrquestarMensajeContexto = AplicacionTest.Infraestructura.LoggerPrueba<PER.Mensajeria.Aplicacion.OrquestarMensajeContexto.OrquestarMensajeContextoAplicacion>;
using RegistroLoggerPrueba = AplicacionTest.Infraestructura.RegistroLoggerPrueba;

namespace AplicacionTest;

public class OrquestarMensajeContextoAplicacionTest
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
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger,
            colaSalida);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();
        DAOMensaje mensajeSalida = await contexto.Mensajes.SingleAsync(
            mensajeActual => mensajeActual.IDDireccionMensaje == "salida");
        DAOEnvioMensaje envio = await contexto.EnviosMensaje.SingleAsync();
        EventoMensajeriaSalida eventoSalida = await colaSalida.ConsumirAsync(CancellationToken.None);

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.Procesado, resultado.Tipo);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.Equal(mensaje.IDLineaConversacion, mensajeSalida.IDLineaConversacion);
        Assert.Equal("pendiente", envio.IDEstadoEnvioMensaje);
        Assert.Equal(envio.ID, eventoSalida.IDEnvioMensaje);
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_MultiplesSalidas_DebePersistirYPublicarCadaEnvio(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        MensajeSalienteContexto primero = CrearMensajeSalienteDesdeEntrada(mensaje);
        MensajeSalienteContexto segundo = CrearMensajeSalienteDesdeEntrada(mensaje);
        segundo.Contenido = "segunda respuesta";
        FakeContextoConversacionServicio contextoConversacion =
            FakeContextoConversacionServicio.ConSalidas(primero, segundo);
        RegistroLoggerPrueba registroLogger = new();
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger,
            colaSalida);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(
            procesamiento.ID,
            CancellationToken.None);

        EventoMensajeriaSalida primerEvento = await colaSalida.ConsumirAsync(CancellationToken.None);
        EventoMensajeriaSalida segundoEvento = await colaSalida.ConsumirAsync(CancellationToken.None);
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        List<long> idsEnvios = await contexto.EnviosMensaje
            .OrderBy(envio => envio.ID)
            .Select(envio => envio.ID)
            .ToListAsync();

        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.Procesado, resultado.Tipo);
        Assert.Equal(idsEnvios, new[] { primerEvento.IDEnvioMensaje, segundoEvento.IDEnvioMensaje });
        Assert.Equal(2, await contexto.Mensajes.CountAsync(
            mensajeActual => mensajeActual.IDDireccionMensaje == "salida"));
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_RegistroSalidaFalla_DebeNoPublicarYMarcarProcesamientoError(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion =
            FakeContextoConversacionServicio.ConSalidas(
                CrearMensajeSalienteDesdeEntrada(mensaje));
        RegistroLoggerPrueba registroLogger = new();
        ColaEventosMensajeriaSalidaServicio colaSalida = new();
        UnitOfWorkFactoryPrueba unitOfWorkFactory = new(baseDatos);
        ILogger<OrquestarMensajeContextoAplicacion> logger =
            new LoggerOrquestarMensajeContexto(registroLogger);
        IOrquestarMensajeContextoAplicacion aplicacion =
            new OrquestarMensajeContextoAplicacion(
                unitOfWorkFactory,
                contextoConversacion,
                new RegistrarMensajeSalidaFallaPrueba(),
                colaSalida,
                logger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(
            procesamiento.ID,
            CancellationToken.None);

        using CancellationTokenSource timeoutCola = new(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            colaSalida.ConsumirAsync(timeoutCola.Token));
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado =
            await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.Error, resultado.Tipo);
        Assert.Equal("error", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Empty(await contexto.EnviosMensaje.ToListAsync());
        registroLogger.AssertContieneError("fallo persistencia salida");
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task EjecutarAsync_ContextoDevuelveSinSalidas_DebeNoRegistrarSalidaNiEnvioYMarcarProcesado(MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.SinSalidas, resultado.Tipo);
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.Error, resultado.Tipo);
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.Error, resultado.Tipo);
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

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
    public async Task EjecutarAsync_LoteMensajes_DebeEnviarMensajesOrdenadosYMarcarTodosProcesados(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        (DAOMensaje primerMensaje, DAOProcesamientoInternoMensaje primerProcesamiento) =
            await baseDatos.CrearMensajeEntradaPendienteAsync();
        (DAOMensaje segundoMensaje, DAOProcesamientoInternoMensaje segundoProcesamiento) =
            await CrearMensajeLoteAsync(
                baseDatos,
                primerMensaje.IDLineaConversacion,
                primerMensaje.FechaMensaje.AddSeconds(1),
                primerProcesamiento.FechaCreacion.AddSeconds(1));
        FakeContextoConversacionServicio contextoConversacion =
            FakeContextoConversacionServicio.SinSalidas();
        RegistroLoggerPrueba registroLogger = new();
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(
            [segundoProcesamiento.ID, primerProcesamiento.ID],
            CancellationToken.None);

        SolicitudContextoConversacion solicitud = Assert.IsType<SolicitudContextoConversacion>(
            contextoConversacion.SolicitudRecibida);
        Assert.Equal(primerProcesamiento.ID, solicitud.IDProcesamientoInternoMensaje);
        Assert.Equal(
            [primerProcesamiento.ID, segundoProcesamiento.ID],
            solicitud.IDsProcesamientosInternosMensaje);
        Assert.Equal(
            [primerMensaje.ID, segundoMensaje.ID],
            solicitud.MensajesEntrantes.Select(mensaje => mensaje.IDMensaje));
        Assert.Equal(
            ["hola", "segundo mensaje"],
            solicitud.MensajesEntrantes.Select(mensaje => mensaje.Contenido));

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        string[] estados = await contexto.ProcesamientosInternosMensaje
            .Where(procesamiento =>
                procesamiento.ID == primerProcesamiento.ID
                || procesamiento.ID == segundoProcesamiento.ID)
            .OrderBy(procesamiento => procesamiento.ID)
            .Select(procesamiento => procesamiento.IDEstadoProcesamientoInternoMensaje)
            .ToArrayAsync();

        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.SinSalidas, resultado.Tipo);
        Assert.Equal(["procesado", "procesado"], estados);
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(
            procesamiento.ID,
            CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();
        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.RenovarLinea, resultado.Tipo);
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
            baseDatos,
            contextoConversacion,
            registroLogger);

        ResultadoOrquestarMensajeContexto resultado = await aplicacion.EjecutarAsync(
            procesamiento.ID,
            CancellationToken.None);

        Assert.Equal(ResultadoOrquestarMensajeContextoTipo.Procesado, resultado.Tipo);
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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion, registroLogger);

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
        IOrquestarMensajeContextoAplicacion aplicacion = CrearAplicacion(
            unitOfWorkFactory,
            contextoConversacion,
            registroLogger);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        Assert.Equal(0, unitOfWorkFactory.AlcancesActivos);
        Assert.Equal(unitOfWorkFactory.AlcancesCreados, unitOfWorkFactory.AlcancesDispuestos);
        registroLogger.AssertSinErrores();
    }

    private static IOrquestarMensajeContextoAplicacion CrearAplicacion(
        BaseDatosPrueba baseDatos,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger)
    {
        return CrearAplicacion(
            new UnitOfWorkFactoryPrueba(baseDatos),
            contextoConversacionServicio,
            registroLogger,
            new ColaEventosMensajeriaSalidaServicio());
    }

    private static IOrquestarMensajeContextoAplicacion CrearAplicacion(
        BaseDatosPrueba baseDatos,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger,
        IColaEventosMensajeriaSalidaServicio colaSalida)
    {
        return CrearAplicacion(
            new UnitOfWorkFactoryPrueba(baseDatos),
            contextoConversacionServicio,
            registroLogger,
            colaSalida);
    }

    private static IOrquestarMensajeContextoAplicacion CrearAplicacion(
        UnitOfWorkFactoryPrueba unitOfWorkFactory,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger)
    {
        return CrearAplicacion(
            unitOfWorkFactory,
            contextoConversacionServicio,
            registroLogger,
            new ColaEventosMensajeriaSalidaServicio());
    }

    private static IOrquestarMensajeContextoAplicacion CrearAplicacion(
        UnitOfWorkFactoryPrueba unitOfWorkFactory,
        IContextoConversacionServicio contextoConversacionServicio,
        RegistroLoggerPrueba registroLogger,
        IColaEventosMensajeriaSalidaServicio colaSalida)
    {
        RegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion = new(unitOfWorkFactory);

        ILogger<OrquestarMensajeContextoAplicacion> logger = new LoggerOrquestarMensajeContexto(registroLogger);

        return new OrquestarMensajeContextoAplicacion(
            unitOfWorkFactory,
            contextoConversacionServicio,
            registrarMensajeSalidaAplicacion,
            colaSalida,
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

    private static async Task<(DAOMensaje Mensaje, DAOProcesamientoInternoMensaje Procesamiento)> CrearMensajeLoteAsync(
        BaseDatosPrueba baseDatos,
        long idLineaConversacion,
        DateTime fechaMensaje,
        DateTime fechaProcesamiento)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = idLineaConversacion,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            TelefonoOrigen = "3001234567",
            TelefonoDestino = "6011234567",
            Contenido = "segundo mensaje",
            IdentificadorExternoMensaje = $"lote_{Guid.NewGuid():N}",
            FechaMensaje = fechaMensaje,
            FechaCreacion = fechaMensaje,
            FechaActualizacion = fechaMensaje
        };
        contexto.Mensajes.Add(mensaje);
        await contexto.SaveChangesAsync();

        DAOProcesamientoInternoMensaje procesamiento = new()
        {
            IDMensaje = mensaje.ID,
            IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
            IDEstadoProcesamientoInternoMensaje = "pendiente",
            FechaCreacion = fechaProcesamiento
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        return (mensaje, procesamiento);
    }

    private sealed class RegistrarMensajeSalidaFallaPrueba
        : IRegistrarMensajeSalidaAplicacion
    {
        public Task<ResultadoRegistrarMensajeSalida> EjecutarAsync(
            SolicitudRegistrarMensajeSalida solicitud,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("fallo persistencia salida");
        }
    }
}
