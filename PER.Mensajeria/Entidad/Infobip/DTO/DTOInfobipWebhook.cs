using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipWebhook
{
    [JsonPropertyName("results")]
    public List<DTOInfobipResult>? Results { get; set; }

    [JsonPropertyName("messageCount")]
    public int? MessageCount { get; set; }

    [JsonPropertyName("pendingMessageCount")]
    public int? PendingMessageCount { get; set; }
}
