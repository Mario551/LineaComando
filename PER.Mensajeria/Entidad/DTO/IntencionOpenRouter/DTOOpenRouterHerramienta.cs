using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterHerramienta
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = "function";

    [JsonPropertyName("function")]
    public DTOOpenRouterFuncion Funcion { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
