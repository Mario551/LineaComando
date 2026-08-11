using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContactOrganization
{
    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
