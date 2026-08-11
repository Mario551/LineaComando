using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContactEmail
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
