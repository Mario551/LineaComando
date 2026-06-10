using AplicacionTest.Fakes;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.API.Contexto;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class OrquestarMensajeEntradaAplicacionTest
{
    [Fact]
    public async Task EjecutarAsync_ContextoDevuelveSalidas_DebeRegistrarSalidaEnvioYMarcarProcesado()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        DTOMensajeSaliente mensajeSaliente = await CrearMensajeSalienteDesdeEntradaAsync(baseDatos, mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConSalidas(mensajeSaliente);
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.True(await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida") > 0);
        Assert.True(await contexto.EnviosMensaje.CountAsync(envioActual => envioActual.IDEstadoEnvioMensaje == "pendiente") > 0);
    }

    [Fact]
    public async Task EjecutarAsync_ContextoDevuelveSinSalidas_DebeNoRegistrarSalidaNiEnvioYMarcarProcesado()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal("procesado", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(procesamientoActualizado.FechaProcesado);
        Assert.Null(procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
    }

    [Fact]
    public async Task EjecutarAsync_ContextoDevuelveError_DebeNoCrearSalidaYMarcarProcesamientoError()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConError("Error final del contexto.");
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal("error", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(1, procesamientoActualizado.Intentos);
        Assert.Contains("Error final del contexto", procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
    }

    [Fact]
    public async Task EjecutarAsync_ContextoLanzaExcepcion_DebeNoCrearSalidaYMarcarProcesamientoError()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConExcepcion(new InvalidOperationException("Fallo contexto fake."));
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOProcesamientoInternoMensaje procesamientoActualizado = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(contextoConversacion.Ejecutado);
        Assert.Equal("error", procesamientoActualizado.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal(1, procesamientoActualizado.Intentos);
        Assert.Contains("Fallo contexto fake", procesamientoActualizado.Error);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(0, await contexto.EnviosMensaje.CountAsync());
    }

    [Fact]
    public async Task EjecutarAsync_DebeEnviarSolicitudContextoConIDsYDatosCargadosDesdeBD()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.SinSalidas();
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        DTOContextoConversacionSolicitud solicitud = Assert.IsType<DTOContextoConversacionSolicitud>(contextoConversacion.SolicitudRecibida);
        Assert.Equal(procesamiento.ID, solicitud.IDProcesamientoInternoMensaje);
        Assert.Equal(mensaje.ID, solicitud.IDMensaje);
        Assert.Equal(mensaje.IDLineaConversacion, solicitud.IDLineaConversacion);
        Assert.Equal(mensaje.IDTipoMensaje, solicitud.TipoMensaje);
        Assert.Equal(mensaje.TelefonoOrigen, solicitud.TelefonoOrigen);
        Assert.Equal(mensaje.TelefonoDestino, solicitud.TelefonoDestino);
        Assert.Equal(mensaje.Contenido, solicitud.Contenido);
        Assert.Equal(mensaje.IdentificadorExternoMensaje, solicitud.IdentificadorExternoMensaje);
        Assert.Equal(mensaje.FechaMensaje, solicitud.FechaMensaje);
    }

    [Fact]
    public async Task EjecutarAsync_ContextoSimulaComandoIntermedio_DebeProcesarSoloResultadoFinal()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        DTOMensajeSaliente mensajeSaliente = await CrearMensajeSalienteDesdeEntradaAsync(baseDatos, mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConComandoIntermedio(mensajeSaliente);
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        Assert.Equal(1, contextoConversacion.PasosInternosSimulados);
        Assert.Equal(1, await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida"));
        Assert.Equal(1, await contexto.EnviosMensaje.CountAsync());
    }

    [Fact]
    public async Task EjecutarAsync_ContextoSimulaHistorialIntermedio_DebeProcesarSoloResultadoFinal()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOMensaje mensaje, DAOProcesamientoInternoMensaje procesamiento) = await baseDatos.CrearMensajeEntradaPendienteAsync();
        DTOMensajeSaliente mensajeSaliente = await CrearMensajeSalienteDesdeEntradaAsync(baseDatos, mensaje);
        FakeContextoConversacionServicio contextoConversacion = FakeContextoConversacionServicio.ConHistorialIntermedio(mensajeSaliente);
        IOrquestarMensajeEntradaAplicacion aplicacion = CrearAplicacion(baseDatos, contextoConversacion);

        await aplicacion.EjecutarAsync(procesamiento.ID, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        Assert.Equal(1, contextoConversacion.PasosInternosSimulados);
        Assert.Equal(1, await contexto.Mensajes.CountAsync(mensajeActual => mensajeActual.IDDireccionMensaje == "salida"));
        Assert.Equal(1, await contexto.EnviosMensaje.CountAsync());
    }

    private static IOrquestarMensajeEntradaAplicacion CrearAplicacion(
        PostgreSqlPrueba baseDatos,
        IContextoConversacionServicio contextoConversacionServicio)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);
        RegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion = new(unitOfWork);

        return new OrquestarMensajeEntradaAplicacion(
            unitOfWork,
            contextoConversacionServicio,
            registrarMensajeSalidaAplicacion);
    }

    private static async Task<DTOMensajeSaliente> CrearMensajeSalienteDesdeEntradaAsync(
        PostgreSqlPrueba baseDatos,
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
