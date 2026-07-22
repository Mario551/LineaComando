using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterFuncion
{
    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Descripcion { get; set; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Parametros { get; set; }

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Argumentos { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
