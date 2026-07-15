using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Servicio.Cola;
using PER.Mensajeria.Servicio.Contexto;
using PER.Mensajeria.Servicio.Orquestador;
using ServicioTest.Fakes;
using LoggerOrquestadorContexto = ServicioTest.Infraestructura.LoggerPrueba<PER.Mensajeria.Servicio.Orquestador.OrquestadorContextoServicio>;
using RegistroLoggerPrueba = ServicioTest.Infraestructura.RegistroLoggerPrueba;

namespace ServicioTest;

public class OrquestadorContextoServicioTest
{
    [Fact]
    public async Task ProcesarAsync_EventoEntrada_DebeEjecutarPasosFuncionalesDelOrquestador()
    {
        FakeOrquestarMensajeEntradaAplicacion aplicacion = new();
        RegistroLoggerPrueba registroLogger = new();
        ILogger<OrquestadorContextoServicio> logger = new LoggerOrquestadorContexto(registroLogger);
        IOrquestadorContextoServicio servicio = new OrquestadorContextoServicio(
            aplicacion,
            new ContextoConversacionActivoServicio(),
            new FakeMensajeServicio(),
            logger);
        EventoMensajeria evento = CrearEvento();

        await servicio.ProcesarAsync(evento, CancellationToken.None);

        Assert.Equal(evento.IDProcesamientoInternoMensaje, aplicacion.IDProcesamientoInternoMensaje);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task ProcesarAsync_ErrorContexto_DebeMarcarProcesamientoComoError()
    {
        FakeOrquestarMensajeEntradaAplicacion aplicacion = new();
        RegistroLoggerPrueba registroLogger = new();
        ILogger<OrquestadorContextoServicio> logger = new LoggerOrquestadorContexto(registroLogger);
        IOrquestadorContextoServicio servicio = new OrquestadorContextoServicio(
            aplicacion,
            new ContextoConversacionActivoServicio(),
            new FakeMensajeServicio(),
            logger);
        EventoMensajeria evento = CrearEvento();

        await servicio.ProcesarAsync(evento, CancellationToken.None);

        Assert.Equal(evento.IDProcesamientoInternoMensaje, aplicacion.IDProcesamientoInternoMensaje);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task ProcesarAsync_RenovarLinea_DebeSolicitarRenovacionAMensajeServicio()
    {
        ResultadoCompactacionIntencionContexto compactacion = ResultadoCompactacionIntencionContexto.Exito(
            "snapshot",
            new MetadataRazonamientoIAContexto
            {
                Proveedor = "fake",
                Modelo = "fake",
                Adaptador = "fake",
                AccionDecidida = "Compactar"
            });
        FakeOrquestarMensajeEntradaAplicacion aplicacion = new()
        {
            Resultado = ResultadoOrquestarMensajeEntrada.RenovarLinea(compactacion)
        };
        FakeMensajeServicio mensajeServicio = new();
        RegistroLoggerPrueba registroLogger = new();
        ILogger<OrquestadorContextoServicio> logger = new LoggerOrquestadorContexto(registroLogger);
        IOrquestadorContextoServicio servicio = new OrquestadorContextoServicio(
            aplicacion,
            new ContextoConversacionActivoServicio(),
            mensajeServicio,
            logger);
        EventoMensajeria evento = CrearEvento();

        await servicio.ProcesarAsync(evento, CancellationToken.None);

        Assert.NotNull(mensajeServicio.SolicitudRenovacion);
        Assert.Equal(evento.IDProcesamientoInternoMensaje, mensajeServicio.SolicitudRenovacion.IDProcesamientoInternoMensaje);
        Assert.Equal(evento.IDMensaje, mensajeServicio.SolicitudRenovacion.IDMensaje);
        Assert.Equal(evento.IDConversacion, mensajeServicio.SolicitudRenovacion.IDConversacion);
        Assert.Equal(evento.IDLineaConversacion, mensajeServicio.SolicitudRenovacion.IDLineaConversacionOrigen);
        Assert.Same(compactacion, mensajeServicio.SolicitudRenovacion.Compactacion);
        registroLogger.AssertSinErrores();
    }

    private static EventoMensajeria CrearEvento()
    {
        return new EventoMensajeria
        {
            IDMensaje = 1,
            IDProcesamientoInternoMensaje = 2,
            IDConversacion = 3,
            IDLineaConversacion = 4,
            FechaCreacion = DateTime.Now
        };
    }
}
