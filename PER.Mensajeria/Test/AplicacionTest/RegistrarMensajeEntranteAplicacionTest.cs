using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class RegistrarMensajeEntranteAplicacionTest
{
    [Fact]
    public async Task EjecutarAsync_MensajeNuevo_DebePersistirMensajeYCrearProcesamientoPendiente()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        string cuenta = $"cuenta_{Guid.NewGuid():N}";
        await baseDatos.CrearCuentaCanalAsync(cuenta);
        IRegistrarMensajeEntranteAplicacion aplicacion = CrearAplicacion(baseDatos);
        DTORegistrarMensajeEntranteSolicitud solicitud = CrearSolicitud(cuenta, "externo-entrada-1");

        DTORegistrarMensajeEntranteRespuesta respuesta = await aplicacion.EjecutarAsync(solicitud, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOMensaje mensaje = await contexto.Mensajes.SingleAsync();
        DAOProcesamientoInternoMensaje procesamiento = await contexto.ProcesamientosInternosMensaje.SingleAsync();

        Assert.True(respuesta.Registrado);
        Assert.Equal(mensaje.ID, respuesta.IDMensaje);
        Assert.Equal(procesamiento.ID, respuesta.IDProcesamientoInternoMensaje);
        Assert.Equal("entrada", mensaje.IDDireccionMensaje);
        Assert.Equal("texto", mensaje.IDTipoMensaje);
        Assert.Equal("pendiente", procesamiento.IDEstadoProcesamientoInternoMensaje);
        Assert.Equal("orquestar_entrada", procesamiento.IDTipoProcesamientoInternoMensaje);
        Assert.Equal(1, await contexto.Conversaciones.CountAsync());
        Assert.Equal(1, await contexto.LineasConversacion.CountAsync());
        Assert.Equal(1, await contexto.ParticipantesConversacion.CountAsync());
        Assert.Equal(1, await contexto.ConversacionesParticipantes.CountAsync());
    }

    [Fact]
    public async Task EjecutarAsync_MensajeDuplicado_NoDebeDuplicarMensajeNiProcesamiento()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        string cuenta = $"cuenta_{Guid.NewGuid():N}";
        await baseDatos.CrearCuentaCanalAsync(cuenta);
        IRegistrarMensajeEntranteAplicacion aplicacion = CrearAplicacion(baseDatos);
        DTORegistrarMensajeEntranteSolicitud solicitud = CrearSolicitud(cuenta, "externo-duplicado-1");

        DTORegistrarMensajeEntranteRespuesta primeraRespuesta = await aplicacion.EjecutarAsync(solicitud, CancellationToken.None);
        DTORegistrarMensajeEntranteRespuesta segundaRespuesta = await aplicacion.EjecutarAsync(solicitud, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();

        Assert.True(primeraRespuesta.Registrado);
        Assert.False(segundaRespuesta.Registrado);
        Assert.Equal(primeraRespuesta.IDMensaje, segundaRespuesta.IDMensaje);
        Assert.Equal(primeraRespuesta.IDProcesamientoInternoMensaje, segundaRespuesta.IDProcesamientoInternoMensaje);
        Assert.Equal(1, await contexto.Mensajes.CountAsync());
        Assert.Equal(1, await contexto.ProcesamientosInternosMensaje.CountAsync());
    }

    [Fact]
    public async Task EjecutarAsync_MensajeMultimedia_DebeRegistrarReferenciaDeArchivo()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        string cuenta = $"cuenta_{Guid.NewGuid():N}";
        await baseDatos.CrearCuentaCanalAsync(cuenta);
        IRegistrarMensajeEntranteAplicacion aplicacion = CrearAplicacion(baseDatos);
        DTORegistrarMensajeEntranteSolicitud solicitud = CrearSolicitud(cuenta, "externo-multimedia-1");

        solicitud.Mensaje.Archivos.Add(new DTOArchivoMensaje
        {
            TipoContenido = "image/png",
            UbicacionArchivo = "s3://mensajes/imagen.png",
            ProveedorAlmacenamiento = "s3"
        });

        DTORegistrarMensajeEntranteRespuesta respuesta = await aplicacion.EjecutarAsync(solicitud, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOArchivoMensaje archivo = await contexto.ArchivosMensaje.SingleAsync();

        Assert.True(respuesta.Registrado);
        Assert.Equal(respuesta.IDMensaje, archivo.IDMensaje);
        Assert.Equal("image/png", archivo.IDTipoContenidoArchivo);
        Assert.Equal("s3://mensajes/imagen.png", archivo.UbicacionArchivo);
        Assert.Equal("s3", archivo.ProveedorAlmacenamiento);
    }

    [Fact]
    public async Task EjecutarAsync_LineaActivaDentroDelUmbral_DebeReutilizarLinea()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        string cuenta = $"cuenta_{Guid.NewGuid():N}";
        await baseDatos.CrearCuentaCanalAsync(cuenta);
        IRegistrarMensajeEntranteAplicacion primeraAplicacion = CrearAplicacion(baseDatos, TimeSpan.FromHours(1));
        DTORegistrarMensajeEntranteSolicitud primeraSolicitud = CrearSolicitud(cuenta, "externo-linea-vigente-1");
        DTORegistrarMensajeEntranteRespuesta primeraRespuesta = await primeraAplicacion.EjecutarAsync(primeraSolicitud, CancellationToken.None);

        await CambiarFechaUltimaActividadLineaAsync(baseDatos, primeraRespuesta.IDLineaConversacion, DateTime.Now.AddMinutes(-15));

        IRegistrarMensajeEntranteAplicacion segundaAplicacion = CrearAplicacion(baseDatos, TimeSpan.FromHours(1));
        DTORegistrarMensajeEntranteSolicitud segundaSolicitud = CrearSolicitud(cuenta, "externo-linea-vigente-2");
        DTORegistrarMensajeEntranteRespuesta segundaRespuesta = await segundaAplicacion.EjecutarAsync(segundaSolicitud, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();

        Assert.Equal(primeraRespuesta.IDLineaConversacion, segundaRespuesta.IDLineaConversacion);
        Assert.Equal(1, await contexto.LineasConversacion.CountAsync());
        Assert.Equal(1, await contexto.LineasConversacion.CountAsync(lineaActual => lineaActual.Activa));
    }

    [Fact]
    public async Task EjecutarAsync_LineaActivaFueraDelUmbral_DebeInactivarLineaAnteriorYCrearNueva()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        string cuenta = $"cuenta_{Guid.NewGuid():N}";
        await baseDatos.CrearCuentaCanalAsync(cuenta);
        IRegistrarMensajeEntranteAplicacion primeraAplicacion = CrearAplicacion(baseDatos, TimeSpan.FromMinutes(30));
        DTORegistrarMensajeEntranteSolicitud primeraSolicitud = CrearSolicitud(cuenta, "externo-linea-antigua-1");
        DTORegistrarMensajeEntranteRespuesta primeraRespuesta = await primeraAplicacion.EjecutarAsync(primeraSolicitud, CancellationToken.None);

        await CambiarFechaUltimaActividadLineaAsync(baseDatos, primeraRespuesta.IDLineaConversacion, DateTime.Now.AddHours(-2));

        IRegistrarMensajeEntranteAplicacion segundaAplicacion = CrearAplicacion(baseDatos, TimeSpan.FromMinutes(30));
        DTORegistrarMensajeEntranteSolicitud segundaSolicitud = CrearSolicitud(cuenta, "externo-linea-antigua-2");
        DTORegistrarMensajeEntranteRespuesta segundaRespuesta = await segundaAplicacion.EjecutarAsync(segundaSolicitud, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOLineaConversacion lineaAnterior = await contexto.LineasConversacion.SingleAsync(lineaActual => lineaActual.ID == primeraRespuesta.IDLineaConversacion);
        DAOLineaConversacion lineaNueva = await contexto.LineasConversacion.SingleAsync(lineaActual => lineaActual.ID == segundaRespuesta.IDLineaConversacion);

        Assert.NotEqual(primeraRespuesta.IDLineaConversacion, segundaRespuesta.IDLineaConversacion);
        Assert.False(lineaAnterior.Activa);
        Assert.True(lineaNueva.Activa);
        Assert.Equal(2, await contexto.LineasConversacion.CountAsync());
        Assert.Equal(1, await contexto.LineasConversacion.CountAsync(lineaActual => lineaActual.Activa));
    }

    private static IRegistrarMensajeEntranteAplicacion CrearAplicacion(
        PostgreSqlPrueba baseDatos,
        TimeSpan? tiempoMaximoInactividad = null)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);
        ConfiguracionLineaConversacion configuracion = new()
        {
            TiempoMaximoInactividad = tiempoMaximoInactividad ?? TimeSpan.FromHours(24)
        };

        return new RegistrarMensajeEntranteAplicacion(unitOfWork, configuracion);
    }

    private static async Task CambiarFechaUltimaActividadLineaAsync(
        PostgreSqlPrueba baseDatos,
        long idLineaConversacion,
        DateTime fechaUltimaActividad)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOLineaConversacion linea = await contexto.LineasConversacion.SingleAsync(
            lineaActual => lineaActual.ID == idLineaConversacion);

        linea.FechaUltimaActividad = fechaUltimaActividad;
        await contexto.SaveChangesAsync();
    }

    private static DTORegistrarMensajeEntranteSolicitud CrearSolicitud(string cuenta, string identificadorExternoMensaje)
    {
        return new DTORegistrarMensajeEntranteSolicitud
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = "whatsapp",
                Cuenta = cuenta,
                IdentificadorParticipante = "3001234567",
                TipoParticipante = "telefono",
                TipoMensaje = "texto",
                TelefonoOrigen = "3001234567",
                TelefonoDestino = "6011234567",
                Contenido = "hola",
                IdentificadorExternoMensaje = identificadorExternoMensaje,
                FechaMensaje = DateTime.Now
            }
        };
    }
}
