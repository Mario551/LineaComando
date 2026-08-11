using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipReferredProduct
{
    [JsonPropertyName("catalogId")]
    public required string CatalogId { get; set; }

    [JsonPropertyName("productRetailerId")]
    public required string ProductRetailerId { get; set; }
}
