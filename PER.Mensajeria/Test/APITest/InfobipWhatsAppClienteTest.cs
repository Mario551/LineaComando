using System.Net;
using System.Net.Http.Headers;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace APITest;

public class InfobipWhatsAppClienteTest
{
    [Fact]
    public async Task EnviarAsync_RespuestaAceptada_DebeConservarContratoCompleto()
    {
        PeticionCapturada peticion = new();
        HttpMessageHandler handler = new HandlerPrueba(async (request, cancellationToken) =>
        {
            peticion.Uri = request.RequestUri;
            peticion.Autorizacion = request.Headers.Authorization;
            peticion.Cuerpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "to": "573163432479",
                      "messageCount": 1,
                      "messageId": "infobip-123",
                      "status": {
                        "groupId": 1,
                        "groupName": "PENDING",
                        "id": 7,
                        "name": "PENDING_ENROUTE",
                        "description": "Message sent to next instance"
                      }
                    }
                    """)
            };
        });
        InfobipWhatsAppCliente cliente = CrearCliente(handler);
        DTOInfobipSolicitudEnvio solicitud = new()
        {
            Endpoint = "/whatsapp/1/message/text",
            CuerpoJson = "{\"from\":\"573213155912\"}"
        };

        DTOResultadoEnvioInfobipCliente resultado = await cliente.EnviarAsync(
            solicitud,
            CancellationToken.None);

        Assert.True(resultado.EsExitosoHttp);
        Assert.Equal(200, resultado.StatusHttp);
        Assert.Equal("infobip-123", resultado.Respuesta?.MessageId);
        Assert.Equal("PENDING_ENROUTE", resultado.Respuesta?.Status?.Name);
        Assert.Equal("https://prueba.api.infobip.com/whatsapp/1/message/text", peticion.Uri?.AbsoluteUri);
        Assert.Equal("App", peticion.Autorizacion?.Scheme);
        Assert.Equal("api-key-prueba", peticion.Autorizacion?.Parameter);
        Assert.Equal(solicitud.CuerpoJson, peticion.Cuerpo);
    }

    [Fact]
    public async Task EnviarAsync_ErrorHttp_DebeConservarStatusYCuerpo()
    {
        const string cuerpo = "{\"message\":\"Invalid destination\"}";
        HttpMessageHandler handler = new HandlerPrueba((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(cuerpo)
            }));
        InfobipWhatsAppCliente cliente = CrearCliente(handler);

        DTOResultadoEnvioInfobipCliente resultado = await cliente.EnviarAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.False(resultado.EsExitosoHttp);
        Assert.Equal(400, resultado.StatusHttp);
        Assert.Equal(cuerpo, resultado.CuerpoRespuesta);
        Assert.Equal("Invalid destination", resultado.ErrorRespuesta?.Message);
    }

    [Fact]
    public async Task EnviarAsync_RespuestaExitosaConJsonInvalido_DebeConservarCuerpoYError()
    {
        const string cuerpo = "{json-invalido";
        HttpMessageHandler handler = new HandlerPrueba((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(cuerpo)
            }));
        InfobipWhatsAppCliente cliente = CrearCliente(handler);

        DTOResultadoEnvioInfobipCliente resultado = await cliente.EnviarAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.True(resultado.EsExitosoHttp);
        Assert.Equal(200, resultado.StatusHttp);
        Assert.Equal(cuerpo, resultado.CuerpoRespuesta);
        Assert.Null(resultado.Respuesta);
        Assert.True(resultado.EsResultadoIncierto);
        Assert.Contains("JSON inválido", resultado.ErrorTecnico);
    }

    [Fact]
    public async Task EnviarAsync_RespuestaExitosaVacia_DebeMarcarResultadoIncierto()
    {
        HttpMessageHandler handler = new HandlerPrueba((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            }));
        InfobipWhatsAppCliente cliente = CrearCliente(handler);

        DTOResultadoEnvioInfobipCliente resultado = await cliente.EnviarAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.True(resultado.EsExitosoHttp);
        Assert.True(resultado.EsResultadoIncierto);
        Assert.Null(resultado.Respuesta);
        Assert.Contains("sin contenido", resultado.ErrorTecnico);
    }

    [Fact]
    public async Task EnviarAsync_ErrorDeRed_DebeRetornarResultadoIncierto()
    {
        HttpMessageHandler handler = new HandlerPrueba((_, _) =>
            throw new HttpRequestException("servidor no disponible"));
        InfobipWhatsAppCliente cliente = CrearCliente(handler);

        DTOResultadoEnvioInfobipCliente resultado = await cliente.EnviarAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.True(resultado.EsResultadoIncierto);
        Assert.Contains("servidor no disponible", resultado.ErrorTecnico);
    }

    [Fact]
    public async Task EnviarAsync_Timeout_DebeDistinguirResultadoIncierto()
    {
        HttpMessageHandler handler = new HandlerPrueba(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        ConfiguracionClienteInfobip configuracion = new(
            new Uri("https://prueba.api.infobip.com"),
            "api-key-prueba")
        {
            Timeout = TimeSpan.FromMilliseconds(30)
        };
        InfobipWhatsAppCliente cliente = new(new HttpClient(handler), configuracion);

        DTOResultadoEnvioInfobipCliente resultado = await cliente.EnviarAsync(
            CrearSolicitud(),
            CancellationToken.None);

        Assert.True(resultado.EsTimeout);
        Assert.True(resultado.EsResultadoIncierto);
    }

    [Fact]
    public async Task EnviarAsync_CancelacionHost_DebePropagarCancelacion()
    {
        HttpMessageHandler handler = new HandlerPrueba(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        InfobipWhatsAppCliente cliente = CrearCliente(handler);
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cliente.EnviarAsync(CrearSolicitud(), cancellationTokenSource.Token));
    }

    private static InfobipWhatsAppCliente CrearCliente(HttpMessageHandler handler)
    {
        ConfiguracionClienteInfobip configuracion = new(
            new Uri("https://prueba.api.infobip.com"),
            "api-key-prueba");
        return new InfobipWhatsAppCliente(new HttpClient(handler), configuracion);
    }

    private static DTOInfobipSolicitudEnvio CrearSolicitud()
    {
        return new DTOInfobipSolicitudEnvio
        {
            Endpoint = "/whatsapp/1/message/text",
            CuerpoJson = "{\"from\":\"573213155912\"}"
        };
    }

    private sealed class HandlerPrueba : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

        public HandlerPrueba(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return responder(request, cancellationToken);
        }
    }

    private sealed class PeticionCapturada
    {
        public Uri? Uri { get; set; }
        public AuthenticationHeaderValue? Autorizacion { get; set; }
        public string? Cuerpo { get; set; }
    }
}
