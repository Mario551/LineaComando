using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipIdentity
{
    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; set; }

    [JsonPropertyName("hash")]
    public required string Hash { get; set; }

    [JsonPropertyName("createdAt")]
    public required string CreatedAt { get; set; }
}
