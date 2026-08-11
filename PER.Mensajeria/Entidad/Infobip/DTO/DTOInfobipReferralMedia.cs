using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipReferralMedia
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }
}
