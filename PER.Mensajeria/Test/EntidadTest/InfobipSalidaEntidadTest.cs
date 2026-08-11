using System.Text.Json;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace EntidadTest;

public class InfobipSalidaEntidadTest
{
    [Fact]
    public void DTOInfobipRespuestaEnvio_DebeDeserializarIdentificadorYEstado()
    {
        DTOInfobipRespuestaEnvio? respuesta = JsonSerializer.Deserialize<
            DTOInfobipRespuestaEnvio>(
            """
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
            """);

        Assert.NotNull(respuesta);
        Assert.Equal("infobip-123", respuesta.MessageId);
        Assert.Equal(1, respuesta.MessageCount);
        Assert.Equal("PENDING", respuesta.Status?.GroupName);
        Assert.Equal("PENDING_ENROUTE", respuesta.Status?.Name);
    }

    [Fact]
    public void DAOIntentoEnvioMensajeInfobip_DebeConservarTrazabilidadTecnica()
    {
        DAOIntentoEnvioMensajeInfobip intento = new()
        {
            IDEnvioMensaje = 41,
            NumeroIntento = 2,
            IDEstado = "aceptado",
            Endpoint = "/whatsapp/1/message/text",
            StatusHttp = 200,
            MessageIDInfobip = "infobip-123"
        };

        Assert.Equal(41, intento.IDEnvioMensaje);
        Assert.Equal(2, intento.NumeroIntento);
        Assert.Equal("aceptado", intento.IDEstado);
        Assert.Equal(200, intento.StatusHttp);
        Assert.Equal("infobip-123", intento.MessageIDInfobip);
    }

    [Fact]
    public void DTOEnvioMensajePendiente_DebeConservarDestinatarioDeTransporte()
    {
        DTOEnvioMensajePendiente envio = new()
        {
            TipoDestinatario = "telefono",
            IdentificadorDestinatario = "573163432479"
        };

        Assert.Equal("telefono", envio.TipoDestinatario);
        Assert.Equal("573163432479", envio.IdentificadorDestinatario);
    }
}
