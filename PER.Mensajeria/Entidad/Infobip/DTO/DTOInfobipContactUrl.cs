using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContactUrl
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
