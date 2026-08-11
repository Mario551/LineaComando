using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContactName
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("middleName")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("nameSuffix")]
    public string? NameSuffix { get; set; }

    [JsonPropertyName("namePrefix")]
    public string? NamePrefix { get; set; }

    [JsonPropertyName("formattedName")]
    public string? FormattedName { get; set; }
}
