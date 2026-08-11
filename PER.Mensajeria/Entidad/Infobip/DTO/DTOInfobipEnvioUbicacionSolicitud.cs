using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipEnvioUbicacionSolicitud
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public DTOInfobipContenidoUbicacion Content { get; set; } = new();

    [JsonPropertyName("callbackData")]
    public string? CallbackData { get; set; }
}
