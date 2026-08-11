using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContactProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("parentUserId")]
    public string? ParentUserId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
