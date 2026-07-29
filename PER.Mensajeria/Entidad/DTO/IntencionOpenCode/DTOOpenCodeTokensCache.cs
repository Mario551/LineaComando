using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeTokensCache
{
    [JsonPropertyName("read")]
    public int Lectura { get; set; }

    [JsonPropertyName("write")]
    public int Escritura { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
