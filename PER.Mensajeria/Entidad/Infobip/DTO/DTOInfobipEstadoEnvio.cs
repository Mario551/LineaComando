using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipEstadoEnvio
{
    [JsonPropertyName("groupId")]
    public int? GroupId { get; set; }

    [JsonPropertyName("groupName")]
    public string? GroupName { get; set; }

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
