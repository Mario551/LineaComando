using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContext
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [JsonPropertyName("referredProduct")]
    public DTOInfobipReferredProduct? ReferredProduct { get; set; }
}
