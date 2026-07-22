using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterMensaje
{
    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Contenido { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IDLlamadaHerramienta { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nombre { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DTOOpenRouterLlamadaHerramienta>? LlamadasHerramientas { get; set; }

    [JsonPropertyName("reasoning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Razonamiento { get; set; }

    [JsonPropertyName("reasoning_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? DetallesRazonamiento { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
