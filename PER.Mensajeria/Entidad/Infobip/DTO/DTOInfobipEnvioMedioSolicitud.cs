using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipEnvioMedioSolicitud
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public DTOInfobipContenidoMedio Content { get; set; } = new();

    [JsonPropertyName("callbackData")]
    public string? CallbackData { get; set; }
}
