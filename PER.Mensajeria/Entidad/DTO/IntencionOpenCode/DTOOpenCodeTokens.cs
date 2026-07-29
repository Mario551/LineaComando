using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeTokens
{
    [JsonPropertyName("input")]
    public int Entrada { get; set; }

    [JsonPropertyName("output")]
    public int Salida { get; set; }

    [JsonPropertyName("reasoning")]
    public int Razonamiento { get; set; }

    [JsonPropertyName("cache")]
    public DTOOpenCodeTokensCache? Cache { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
