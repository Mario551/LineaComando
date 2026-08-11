using System.Text.Json;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace APITest;

public class AdaptadorMensajeSalidaInfobipTest
{
    [Theory]
    [InlineData("texto", "/whatsapp/1/message/text")]
    [InlineData("imagen", "/whatsapp/1/message/image")]
    [InlineData("audio", "/whatsapp/1/message/audio")]
    [InlineData("video", "/whatsapp/1/message/video")]
    [InlineData("documento", "/whatsapp/1/message/document")]
    [InlineData("ubicacion", "/whatsapp/1/message/location")]
    public void Adaptar_TipoSoportado_DebeConstruirSolicitud(
        string tipoMensaje,
        string endpoint)
    {
        AdaptadorMensajeSalidaInfobip adaptador = new();
        DTOEnvioMensajePendiente mensaje = CrearMensaje(tipoMensaje);

        DTOResultadoAdaptacionEnvioInfobip resultado = adaptador.Adaptar(mensaje);

        Assert.True(resultado.Exitosa, resultado.Error);
        Assert.NotNull(resultado.Solicitud);
        Assert.Equal(endpoint, resultado.Solicitud.Endpoint);
        using JsonDocument documento = JsonDocument.Parse(resultado.Solicitud.CuerpoJson);
        JsonElement raiz = documento.RootElement;
        Assert.Equal("573213155912", raiz.GetProperty("from").GetString());
        Assert.Equal("573163432479", raiz.GetProperty("to").GetString());
        Assert.Equal("41", raiz.GetProperty("callbackData").GetString());
        Assert.True(raiz.TryGetProperty("content", out JsonElement contenido));

        if (tipoMensaje == "texto")
        {
            Assert.Equal("Mensaje de prueba", contenido.GetProperty("text").GetString());
        }
        else if (tipoMensaje == "ubicacion")
        {
            Assert.Equal(4.710989, contenido.GetProperty("latitude").GetDouble(), 6);
            Assert.Equal(-74.07209, contenido.GetProperty("longitude").GetDouble(), 6);
        }
        else
        {
            Assert.Equal(
                "https://archivos.example.com/archivo.pdf",
                contenido.GetProperty("mediaUrl").GetString());
        }
    }

    [Fact]
    public void Adaptar_MedioSinHttps_DebeFallarSinSolicitud()
    {
        AdaptadorMensajeSalidaInfobip adaptador = new();
        DTOEnvioMensajePendiente mensaje = CrearMensaje("imagen");
        mensaje.Mensaje.Archivos[0].UbicacionArchivo =
            "http://archivos.example.com/imagen.jpg";

        DTOResultadoAdaptacionEnvioInfobip resultado = adaptador.Adaptar(mensaje);

        Assert.False(resultado.Exitosa);
        Assert.Null(resultado.Solicitud);
        Assert.Contains("HTTPS", resultado.Error);
    }

    [Fact]
    public void Adaptar_DestinatarioNoTelefonico_DebeFallar()
    {
        AdaptadorMensajeSalidaInfobip adaptador = new();
        DTOEnvioMensajePendiente mensaje = CrearMensaje("texto");
        mensaje.TipoDestinatario = "usuario";

        DTOResultadoAdaptacionEnvioInfobip resultado = adaptador.Adaptar(mensaje);

        Assert.False(resultado.Exitosa);
        Assert.Contains("destinatario", resultado.Error);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"latitude\":4.710989}")]
    [InlineData("{\"longitude\":-74.07209}")]
    public void Adaptar_UbicacionSinCoordenadasCompletas_NoDebeCrearSolicitud(
        string contenido)
    {
        AdaptadorMensajeSalidaInfobip adaptador = new();
        DTOEnvioMensajePendiente mensaje = CrearMensaje("ubicacion");
        mensaje.Mensaje.Contenido = contenido;

        DTOResultadoAdaptacionEnvioInfobip resultado = adaptador.Adaptar(mensaje);

        Assert.False(resultado.Exitosa);
        Assert.Null(resultado.Solicitud);
        Assert.Contains("coordenadas", resultado.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static DTOEnvioMensajePendiente CrearMensaje(string tipoMensaje)
    {
        DTOEnvioMensajePendiente mensaje = new()
        {
            IDEnvioMensaje = 41,
            Canal = "whatsapp",
            Cuenta = "573213155912",
            TipoDestinatario = "telefono",
            IdentificadorDestinatario = "573163432479",
            Mensaje = new DTOMensajeSaliente
            {
                TipoMensaje = tipoMensaje,
                Contenido = "Mensaje de prueba"
            }
        };

        if (tipoMensaje == "ubicacion")
        {
            mensaje.Mensaje.Contenido = """
                {
                  "Latitude": 4.710989,
                  "Longitude": -74.07209,
                  "Name": "Bogotá",
                  "Address": "Centro"
                }
                """;
        }
        else if (tipoMensaje is "imagen" or "audio" or "video" or "documento")
        {
            mensaje.Mensaje.Archivos.Add(new DTOArchivoMensaje
            {
                NombreArchivo = "archivo.pdf",
                TipoContenido = "application/pdf",
                UbicacionArchivo = "https://archivos.example.com/archivo.pdf"
            });
        }

        return mensaje;
    }
}
