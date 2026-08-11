using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipRespuestaEnvio
{
    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("messageCount")]
    public int? MessageCount { get; set; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }

    [JsonPropertyName("status")]
    public DTOInfobipEstadoEnvio? Status { get; set; }
}
