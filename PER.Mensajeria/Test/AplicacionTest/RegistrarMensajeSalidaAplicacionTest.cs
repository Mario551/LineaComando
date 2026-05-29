using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

namespace AplicacionTest;

public class RegistrarMensajeSalidaAplicacionTest
{
    [Fact]
    public async Task EjecutarAsync_MensajeSalida_DebeCrearMensajeSalidaYEnvioPendiente()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOCuentaCanal cuenta, DAOConversacion conversacion, DAOLineaConversacion linea) = await baseDatos.CrearConversacionAsync($"cuenta_{Guid.NewGuid():N}");
        IRegistrarMensajeSalidaAplicacion aplicacion = CrearAplicacion(baseDatos);
        DTORegistrarMensajeSalidaSolicitud solicitud = CrearSolicitud(conversacion.ID, linea.ID);

        DTORegistrarMensajeSalidaRespuesta respuesta = await aplicacion.EjecutarAsync(solicitud, CancellationToken.None);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        DAOMensaje mensaje = await contexto.Mensajes.SingleAsync();
        DAOEnvioMensaje envio = await contexto.EnviosMensaje.SingleAsync();

        Assert.True(respuesta.Registrado);
        Assert.Equal(mensaje.ID, respuesta.IDMensaje);
        Assert.Equal(envio.ID, respuesta.IDEnvioMensaje);
        Assert.Equal(linea.ID, mensaje.IDLineaConversacion);
        Assert.Equal("salida", mensaje.IDDireccionMensaje);
        Assert.Equal("pendiente", envio.IDEstadoEnvioMensaje);
    }

    [Fact]
    public async Task EjecutarAsync_LineaNoPerteneceAConversacion_DebeFallar()
    {
        await using PostgreSqlPrueba baseDatos = await PostgreSqlPrueba.CrearAsync();
        (DAOCuentaCanal primeraCuenta, DAOConversacion conversacion, DAOLineaConversacion primeraLinea) = await baseDatos.CrearConversacionAsync($"cuenta_{Guid.NewGuid():N}");
        (DAOCuentaCanal segundaCuenta, DAOConversacion segundaConversacion, DAOLineaConversacion lineaAjena) = await baseDatos.CrearConversacionAsync($"cuenta_{Guid.NewGuid():N}");
        IRegistrarMensajeSalidaAplicacion aplicacion = CrearAplicacion(baseDatos);
        DTORegistrarMensajeSalidaSolicitud solicitud = CrearSolicitud(conversacion.ID, lineaAjena.ID);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            aplicacion.EjecutarAsync(solicitud, CancellationToken.None));

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        Assert.Empty(await contexto.Mensajes.ToListAsync());
        Assert.Empty(await contexto.EnviosMensaje.ToListAsync());
    }

    private static IRegistrarMensajeSalidaAplicacion CrearAplicacion(PostgreSqlPrueba baseDatos)
    {
        MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);

        return new RegistrarMensajeSalidaAplicacion(unitOfWork);
    }

    private static DTORegistrarMensajeSalidaSolicitud CrearSolicitud(long idConversacion, long idLineaConversacion)
    {
        return new DTORegistrarMensajeSalidaSolicitud
        {
            Mensaje = new DTOMensajeSaliente
            {
                IDConversacion = idConversacion,
                IDLineaConversacion = idLineaConversacion,
                TipoMensaje = "texto",
                Contenido = "respuesta",
                FechaMensaje = DateTime.Now
            }
        };
    }
}
