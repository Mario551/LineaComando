using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Infobip.Envio;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Datos.Infobip.Esquema;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace AplicacionTest;

public class RegistrarIntentoEnvioInfobipAplicacionTest
{
    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task IniciarYFinalizar_DebePersistirIntentoAceptado(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await CrearBaseDatosInfobipAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        RegistrarIntentoEnvioInfobipAplicacion aplicacion = new(new UnitOfWork(contexto));
        DTOInfobipSolicitudEnvio solicitud = new()
        {
            Endpoint = "/whatsapp/1/message/text",
            CuerpoJson = "{\"from\":\"573213155912\"}"
        };

        long idIntento = await aplicacion.IniciarAsync(
            envio.ID,
            solicitud,
            CancellationToken.None);
        await aplicacion.FinalizarAsync(
            idIntento,
            "aceptado",
            CrearResultadoAceptado(),
            null,
            CancellationToken.None);

        Assert.Empty(contexto.ChangeTracker.Entries());
        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        DAOIntentoEnvioMensajeInfobip intento = await verificacion
            .IntentosEnvioMensajeInfobip
            .AsNoTracking()
            .SingleAsync(intentoActual => intentoActual.ID == idIntento);
        Assert.Equal(envio.ID, intento.IDEnvioMensaje);
        Assert.Equal(1, intento.NumeroIntento);
        Assert.Equal("aceptado", intento.IDEstado);
        Assert.Equal(solicitud.Endpoint, intento.Endpoint);
        Assert.Equal(solicitud.CuerpoJson, intento.SolicitudJson);
        Assert.Equal(200, intento.StatusHttp);
        Assert.Equal("infobip-123", intento.MessageIDInfobip);
        Assert.Equal(1, intento.IDGrupoEstadoInfobip);
        Assert.Equal("PENDING", intento.GrupoEstadoInfobip);
        Assert.Equal(7, intento.IDEstadoInfobip);
        Assert.Equal("PENDING_ENROUTE", intento.EstadoInfobip);
        Assert.NotNull(intento.FechaFinalizacion);
        Assert.Null(intento.Error);
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task Iniciar_IntentoAnteriorAbierto_DebeMarcarloIncierto(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await CrearBaseDatosInfobipAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();
        DTOInfobipSolicitudEnvio solicitud = new()
        {
            Endpoint = "/whatsapp/1/message/text",
            CuerpoJson = "{}"
        };
        long primerIntento;
        long segundoIntento;

        await using (MensajeriaContextoDB contexto = baseDatos.CrearContexto())
        {
            RegistrarIntentoEnvioInfobipAplicacion aplicacion = new(new UnitOfWork(contexto));
            primerIntento = await aplicacion.IniciarAsync(
                envio.ID,
                solicitud,
                CancellationToken.None);
        }

        await using (MensajeriaContextoDB contexto = baseDatos.CrearContexto())
        {
            RegistrarIntentoEnvioInfobipAplicacion aplicacion = new(new UnitOfWork(contexto));
            segundoIntento = await aplicacion.IniciarAsync(
                envio.ID,
                solicitud,
                CancellationToken.None);
        }

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        List<DAOIntentoEnvioMensajeInfobip> intentos = await verificacion
            .IntentosEnvioMensajeInfobip
            .AsNoTracking()
            .OrderBy(intento => intento.NumeroIntento)
            .ToListAsync();
        Assert.Collection(
            intentos,
            intento =>
            {
                Assert.Equal(primerIntento, intento.ID);
                Assert.Equal("incierto", intento.IDEstado);
                Assert.NotNull(intento.FechaFinalizacion);
            },
            intento =>
            {
                Assert.Equal(segundoIntento, intento.ID);
                Assert.Equal(2, intento.NumeroIntento);
                Assert.Equal("enviando", intento.IDEstado);
                Assert.Null(intento.FechaFinalizacion);
            });
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarFalloAdaptacion_DebeCrearIntentoTerminal(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await CrearBaseDatosInfobipAsync(motor);
        (DAOConversacion _, DAOLineaConversacion _, DAOMensaje _, DAOEnvioMensaje envio) =
            await baseDatos.CrearEnvioPendienteAsync();
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        RegistrarIntentoEnvioInfobipAplicacion aplicacion = new(new UnitOfWork(contexto));

        await aplicacion.RegistrarFalloAdaptacionAsync(
            envio.ID,
            "Mensaje inválido",
            CancellationToken.None);

        await using MensajeriaContextoDB verificacion = baseDatos.CrearContexto();
        DAOIntentoEnvioMensajeInfobip intento = await verificacion
            .IntentosEnvioMensajeInfobip
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("fallido", intento.IDEstado);
        Assert.Equal("Mensaje inválido", intento.Error);
        Assert.Null(intento.Endpoint);
        Assert.NotNull(intento.FechaFinalizacion);
    }

    private static DTOResultadoEnvioInfobipCliente CrearResultadoAceptado()
    {
        return new DTOResultadoEnvioInfobipCliente
        {
            EsExitosoHttp = true,
            StatusHttp = 200,
            CuerpoRespuesta = "{\"messageId\":\"infobip-123\"}",
            Respuesta = new DTOInfobipRespuestaEnvio
            {
                MessageId = "infobip-123",
                Status = new DTOInfobipEstadoEnvio
                {
                    GroupId = 1,
                    GroupName = "PENDING",
                    Id = 7,
                    Name = "PENDING_ENROUTE",
                    Description = "Message sent to next instance"
                }
            }
        };
    }

    private static async Task<BaseDatosPrueba> CrearBaseDatosInfobipAsync(
        MotorBaseDatosPrueba motor)
    {
        BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ConfiguracionInicializacionEsquemaMensajeria configuracion = new()
        {
            Proveedor = motor == MotorBaseDatosPrueba.PostgreSql
                ? ProveedorBaseDatosMensajeria.PostgreSql
                : ProveedorBaseDatosMensajeria.SqlServer,
            CadenaConexion = baseDatos.ConnectionString,
            Esquema = baseDatos.Esquema
        };
        InicializadorModuloEsquemaInfobip inicializador = new();
        await inicializador.InicializarAsync(configuracion, CancellationToken.None);
        return baseDatos;
    }
}
