using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContactPhone
{
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("waId")]
    public string? WaId { get; set; }
}
