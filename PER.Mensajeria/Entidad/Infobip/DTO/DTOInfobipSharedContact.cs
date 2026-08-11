using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipSharedContact
{
    [JsonPropertyName("addresses")]
    public List<DTOInfobipContactAddress>? Addresses { get; set; }

    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    [JsonPropertyName("emails")]
    public List<DTOInfobipContactEmail>? Emails { get; set; }

    [JsonPropertyName("name")]
    public DTOInfobipContactName? Name { get; set; }

    [JsonPropertyName("org")]
    public DTOInfobipContactOrganization? Org { get; set; }

    [JsonPropertyName("phones")]
    public List<DTOInfobipContactPhone>? Phones { get; set; }

    [JsonPropertyName("urls")]
    public List<DTOInfobipContactUrl>? Urls { get; set; }
}
