using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterSolicitudChat
{
    [JsonPropertyName("model")]
    public string Modelo { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<DTOOpenRouterMensaje> Mensajes { get; set; } = [];

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DTOOpenRouterHerramienta>? Herramientas { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EleccionHerramienta { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LlamadasHerramientasParalelas { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Temperatura { get; set; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaximoTokens { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DTOOpenRouterFormatoRespuesta? FormatoRespuesta { get; set; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DTOOpenRouterConfiguracionProveedor? Proveedor { get; set; }

    [JsonPropertyName("reasoning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DTOOpenRouterConfiguracionRazonamiento? Razonamiento { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
