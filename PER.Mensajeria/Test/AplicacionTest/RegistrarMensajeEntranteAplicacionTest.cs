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

    private static IRegistrarMensajeEntranteAplicacion CrearAplicacion(PostgreSqlPrueba baseDatos)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);

        return new RegistrarMensajeEntranteAplicacion(unitOfWork);
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
