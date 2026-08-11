using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipCallPermissionReply
{
    [JsonPropertyName("response")]
    public required string Response { get; set; }

    [JsonPropertyName("expirationTimestamp")]
    public string? ExpirationTimestamp { get; set; }
}
