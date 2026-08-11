using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("requestError")]
    public JsonElement? RequestError { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
