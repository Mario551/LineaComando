using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterConfiguracionProveedor
{
    [JsonPropertyName("only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Solo { get; set; }

    [JsonPropertyName("allow_fallbacks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PermitirAlternativas { get; set; }

    [JsonPropertyName("require_parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequerirParametros { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
