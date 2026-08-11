using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipInThreadAuthenticationReply
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("businessScopedPasskeyHash")]
    public string? BusinessScopedPasskeyHash { get; set; }
}
