using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipPaymentAmount
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}
