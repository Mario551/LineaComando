using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipOrderProductItem
{
    [JsonPropertyName("currency")]
    public required string Currency { get; set; }

    [JsonPropertyName("itemPrice")]
    public decimal ItemPrice { get; set; }

    [JsonPropertyName("productRetailerId")]
    public required string ProductRetailerId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
