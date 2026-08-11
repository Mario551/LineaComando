using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipMessagePrice
{
    [JsonPropertyName("pricePerMessage")]
    public decimal? PricePerMessage { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}
