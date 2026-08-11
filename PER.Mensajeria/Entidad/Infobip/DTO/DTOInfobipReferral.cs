using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipReferral
{
    [JsonPropertyName("sourceType")]
    public required string SourceType { get; set; }

    [JsonPropertyName("sourceId")]
    public string? SourceId { get; set; }

    [JsonPropertyName("sourceUrl")]
    public required string SourceUrl { get; set; }

    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("referralMedia")]
    public DTOInfobipReferralMedia? ReferralMedia { get; set; }

    [JsonPropertyName("ctwaClickId")]
    public string? CtwaClickId { get; set; }
}
