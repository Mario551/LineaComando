using System.Text.Json;
using AplicacionTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Infobip.Mapeo;
using PER.Mensajeria.Aplicacion.Infobip.RegistrarWebhook;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.Esquema;
using PER.Mensajeria.Datos.Infobip.Esquema;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace AplicacionTest;

public class RegistrarWebhookInfobipPersistenciaTest
{
    private const string MessageIdImagen =
        "E_Y7yrDivY2ltVSpvquocz7yyoLhB-nvTpKqjAnpjbUjVDZGZMEt_4ihooV6Uxp2G0BqIBkv4_8rYCpOe8bSIzog";
    private const string MessageIdTexto =
        "E_Y7yrDivY2ltVSpvquocz7yyoLhB-nvTpKqjAnpjbUjW7MnD9leGpy0LNgV3ExEwLHLEvyQST4wGxePS1Js4DDg";
    private const string UrlImagen =
        "https://api.infobip.com/whatsapp/1/senders/573213155912/media/20669_22_2934961203506469";

    private const string JsonImagen = """
        {
            "results": [
                {
                    "from": "573163432479",
                    "to": "573213155912",
                    "integrationType": "WHATSAPP",
                    "receivedAt": "2026-08-04T22:17:49.000+0000",
                    "messageId": "E_Y7yrDivY2ltVSpvquocz7yyoLhB-nvTpKqjAnpjbUjVDZGZMEt_4ihooV6Uxp2G0BqIBkv4_8rYCpOe8bSIzog",
                    "pairedMessageId": null,
                    "callbackData": null,
                    "message": {
                        "url": "https://api.infobip.com/whatsapp/1/senders/573213155912/media/20669_22_2934961203506469",
                        "caption": "Prueba de imagen 1",
                        "type": "IMAGE"
                    },
                    "contact": {
                        "name": "Mario",
                        "phoneNumber": "573163432479",
                        "userId": "CO.1776622120445919",
                        "parentUserId": null,
                        "username": null
                    },
                    "price": {
                        "pricePerMessage": 0.000000,
                        "currency": "USD"
                    }
                }
            ],
            "messageCount": 1,
            "pendingMessageCount": 81
        }
        """;

    private const string JsonTexto = """
        {
            "results": [
                {
                    "from": "573163432479",
                    "to": "573213155912",
                    "integrationType": "WHATSAPP",
                    "receivedAt": "2026-08-04T22:17:00.000+0000",
                    "messageId": "E_Y7yrDivY2ltVSpvquocz7yyoLhB-nvTpKqjAnpjbUjW7MnD9leGpy0LNgV3ExEwLHLEvyQST4wGxePS1Js4DDg",
                    "pairedMessageId": null,
                    "callbackData": null,
                    "message": {
                        "text": "Prueba de texto 1",
                        "type": "TEXT"
                    },
                    "contact": {
                        "name": "Mario",
                        "phoneNumber": "573163432479",
                        "userId": "CO.1776622120445919",
                        "parentUserId": null,
                        "username": null
                    },
                    "price": {
                        "pricePerMessage": 0.000000,
                        "currency": "USD"
                    }
                }
            ],
            "messageCount": 1,
            "pendingMessageCount": 81
        }
        """;

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarImagen_DebePersistirGrafoInfobipCompleto(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await CrearBaseDatosInfobipAsync(motor);
        DTOInfobipWebhook webhook = DeserializarWebhook(JsonImagen);
        DTOInfobipResult resultadoWebhook = ObtenerResultadoUnico(webhook);

        DTOResultadoRecepcionMensajeInfobip resultado = await RegistrarAsync(
            baseDatos,
            resultadoWebhook);

        Assert.True(resultado.Registrado);
        Assert.Equal(MessageIdImagen, resultado.MessageId);
        Assert.Equal("pendiente", resultado.Estado);
        Assert.True(resultado.IDWebhookReceiptInfobip > 0);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        WebhookReceiptInfobip recepcion = await contexto.WebhookReceiptsInfobip
            .AsNoTracking()
            .SingleAsync();
        InboundMessageInfobip mensajeEntrante = await contexto.InboundMessagesInfobip
            .AsNoTracking()
            .SingleAsync();
        ImageMessageInfobip imagen = await contexto.ImageMessagesInfobip
            .AsNoTracking()
            .SingleAsync();
        DAOProcesamientoMensajeEntranteInfobip procesamiento = await contexto
            .ProcesamientosMensajeEntranteInfobip
            .AsNoTracking()
            .SingleAsync();

        AfirmarRecepcionComun(
            recepcion,
            MessageIdImagen,
            new DateTime(2026, 8, 4, 22, 17, 49, DateTimeKind.Unspecified));
        AfirmarRelacionesComunes(recepcion, mensajeEntrante, procesamiento, "IMAGE");
        Assert.True(imagen.RecordId > 0);
        Assert.Equal(mensajeEntrante.RecordId, imagen.RecordIdInboundMessagesInfobip);
        Assert.Equal(UrlImagen, imagen.Url);
        Assert.Equal("Prueba de imagen 1", imagen.Caption);
        Assert.Equal(recepcion.RecordCreatedAt, imagen.RecordCreatedAt);
        Assert.Null(imagen.RecordUpdatedAt);
        Assert.Empty(await contexto.TextMessagesInfobip.AsNoTracking().ToListAsync());
        Assert.Empty(await contexto.Mensajes.AsNoTracking().ToListAsync());
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarTexto_DebePersistirGrafoInfobipCompleto(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await CrearBaseDatosInfobipAsync(motor);
        DTOInfobipWebhook webhook = DeserializarWebhook(JsonTexto);
        DTOInfobipResult resultadoWebhook = ObtenerResultadoUnico(webhook);

        DTOResultadoRecepcionMensajeInfobip resultado = await RegistrarAsync(
            baseDatos,
            resultadoWebhook);

        Assert.True(resultado.Registrado);
        Assert.Equal(MessageIdTexto, resultado.MessageId);
        Assert.Equal("pendiente", resultado.Estado);
        Assert.True(resultado.IDWebhookReceiptInfobip > 0);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        WebhookReceiptInfobip recepcion = await contexto.WebhookReceiptsInfobip
            .AsNoTracking()
            .SingleAsync();
        InboundMessageInfobip mensajeEntrante = await contexto.InboundMessagesInfobip
            .AsNoTracking()
            .SingleAsync();
        TextMessageInfobip texto = await contexto.TextMessagesInfobip
            .AsNoTracking()
            .SingleAsync();
        DAOProcesamientoMensajeEntranteInfobip procesamiento = await contexto
            .ProcesamientosMensajeEntranteInfobip
            .AsNoTracking()
            .SingleAsync();

        AfirmarRecepcionComun(
            recepcion,
            MessageIdTexto,
            new DateTime(2026, 8, 4, 22, 17, 0, DateTimeKind.Unspecified));
        AfirmarRelacionesComunes(recepcion, mensajeEntrante, procesamiento, "TEXT");
        Assert.True(texto.RecordId > 0);
        Assert.Equal(mensajeEntrante.RecordId, texto.RecordIdInboundMessagesInfobip);
        Assert.Equal("Prueba de texto 1", texto.Text);
        Assert.Equal(recepcion.RecordCreatedAt, texto.RecordCreatedAt);
        Assert.Null(texto.RecordUpdatedAt);
        Assert.Empty(await contexto.ImageMessagesInfobip.AsNoTracking().ToListAsync());
        Assert.Empty(await contexto.Mensajes.AsNoTracking().ToListAsync());
    }

    [Theory]
    [MemberData(nameof(BaseDatosPrueba.Motores), MemberType = typeof(BaseDatosPrueba))]
    public async Task RegistrarTextoDosVeces_DebeConservarUnSoloGrafoInfobip(
        MotorBaseDatosPrueba motor)
    {
        await using BaseDatosPrueba baseDatos = await CrearBaseDatosInfobipAsync(motor);
        DTOInfobipWebhook webhook = DeserializarWebhook(JsonTexto);
        DTOInfobipResult resultadoWebhook = ObtenerResultadoUnico(webhook);

        DTOResultadoRecepcionMensajeInfobip primerResultado = await RegistrarAsync(
            baseDatos,
            resultadoWebhook);
        DTOResultadoRecepcionMensajeInfobip segundoResultado = await RegistrarAsync(
            baseDatos,
            resultadoWebhook);

        Assert.True(primerResultado.Registrado);
        Assert.False(segundoResultado.Registrado);
        Assert.Equal(primerResultado.IDWebhookReceiptInfobip, segundoResultado.IDWebhookReceiptInfobip);
        Assert.Equal(MessageIdTexto, segundoResultado.MessageId);
        Assert.Equal("pendiente", segundoResultado.Estado);

        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        Assert.Equal(1, await contexto.WebhookReceiptsInfobip.AsNoTracking().CountAsync());
        Assert.Equal(1, await contexto.InboundMessagesInfobip.AsNoTracking().CountAsync());
        Assert.Equal(1, await contexto.TextMessagesInfobip.AsNoTracking().CountAsync());
        Assert.Equal(1, await contexto.ProcesamientosMensajeEntranteInfobip.AsNoTracking().CountAsync());
        Assert.Empty(await contexto.ImageMessagesInfobip.AsNoTracking().ToListAsync());
        Assert.Empty(await contexto.Mensajes.AsNoTracking().ToListAsync());
    }

    private static DTOInfobipWebhook DeserializarWebhook(string json)
    {
        DTOInfobipWebhook webhook = Assert.IsType<DTOInfobipWebhook>(
            JsonSerializer.Deserialize<DTOInfobipWebhook>(json));
        Assert.Equal(1, webhook.MessageCount);
        Assert.Equal(81, webhook.PendingMessageCount);
        Assert.NotNull(webhook.Results);
        Assert.Single(webhook.Results);
        return webhook;
    }

    private static DTOInfobipResult ObtenerResultadoUnico(DTOInfobipWebhook webhook)
    {
        Assert.NotNull(webhook.Results);
        return Assert.Single(webhook.Results);
    }

    private static async Task<BaseDatosPrueba> CrearBaseDatosInfobipAsync(
        MotorBaseDatosPrueba motor)
    {
        BaseDatosPrueba baseDatos = await BaseDatosPrueba.CrearAsync(motor);
        ProveedorBaseDatosMensajeria proveedor = motor switch
        {
            MotorBaseDatosPrueba.PostgreSql => ProveedorBaseDatosMensajeria.PostgreSql,
            MotorBaseDatosPrueba.SqlServer => ProveedorBaseDatosMensajeria.SqlServer,
            _ => throw new NotSupportedException($"Motor de base de datos no soportado: {motor}.")
        };
        ConfiguracionInicializacionEsquemaMensajeria configuracion = new()
        {
            Proveedor = proveedor,
            CadenaConexion = baseDatos.ConnectionString,
            Esquema = baseDatos.Esquema
        };
        InicializadorModuloEsquemaInfobip inicializador = new();
        await inicializador.InicializarAsync(configuracion, CancellationToken.None);
        return baseDatos;
    }

    private static async Task<DTOResultadoRecepcionMensajeInfobip> RegistrarAsync(
        BaseDatosPrueba baseDatos,
        DTOInfobipResult resultadoWebhook)
    {
        await using MensajeriaContextoDB contexto = baseDatos.CrearContexto();
        UnitOfWork unitOfWork = new(contexto);
        RegistrarWebhookInfobipAplicacion aplicacion = new(
            unitOfWork,
            new MapeadorWebhookInfobipServicio());
        return await aplicacion.EjecutarAsync(resultadoWebhook, CancellationToken.None);
    }

    private static void AfirmarRecepcionComun(
        WebhookReceiptInfobip recepcion,
        string messageId,
        DateTime fechaRecibido)
    {
        Assert.True(recepcion.RecordId > 0);
        Assert.Null(recepcion.EntityId);
        Assert.Null(recepcion.ApplicationId);
        Assert.Equal("573163432479", recepcion.From);
        Assert.Equal("573213155912", recepcion.To);
        Assert.Equal("WHATSAPP", recepcion.IntegrationType);
        Assert.Equal(fechaRecibido, recepcion.ReceivedAt);
        Assert.Equal(DateTimeKind.Unspecified, recepcion.ReceivedAt.Kind);
        Assert.Null(recepcion.Keyword);
        Assert.Equal(messageId, recepcion.MessageId);
        Assert.Null(recepcion.PairedMessageId);
        Assert.Null(recepcion.CallbackData);
        Assert.True(recepcion.PricePerMessage.HasValue);
        Assert.Equal(0m, recepcion.PricePerMessage.GetValueOrDefault());
        Assert.Equal("USD", recepcion.Currency);
        Assert.Equal("Mario", recepcion.Name);
        Assert.Equal("573163432479", recepcion.PhoneNumber);
        Assert.Equal("CO.1776622120445919", recepcion.UserId);
        Assert.Null(recepcion.ParentUserId);
        Assert.Null(recepcion.Username);
        Assert.Null(recepcion.Acknowledged);
        Assert.Null(recepcion.Hash);
        Assert.Null(recepcion.CreatedAt);
        Assert.NotEqual(default, recepcion.RecordCreatedAt);
        Assert.Null(recepcion.RecordUpdatedAt);
    }

    private static void AfirmarRelacionesComunes(
        WebhookReceiptInfobip recepcion,
        InboundMessageInfobip mensajeEntrante,
        DAOProcesamientoMensajeEntranteInfobip procesamiento,
        string tipoMensaje)
    {
        Assert.True(mensajeEntrante.RecordId > 0);
        Assert.Equal(recepcion.RecordId, mensajeEntrante.RecordIdWebhookReceiptsInfobip);
        Assert.Equal(tipoMensaje, mensajeEntrante.Type);
        Assert.Equal(recepcion.RecordCreatedAt, mensajeEntrante.RecordCreatedAt);
        Assert.Null(mensajeEntrante.RecordUpdatedAt);
        Assert.True(procesamiento.ID > 0);
        Assert.Equal(recepcion.RecordId, procesamiento.IDWebhookReceiptInfobip);
        Assert.Equal("pendiente", procesamiento.IDEstado);
        Assert.Null(procesamiento.IDMensaje);
        Assert.Equal(0, procesamiento.Intentos);
        Assert.Null(procesamiento.Error);
        Assert.Equal(recepcion.RecordCreatedAt, procesamiento.FechaCreacion);
        Assert.Null(procesamiento.FechaDespachado);
        Assert.Null(procesamiento.FechaProcesado);
    }
}
