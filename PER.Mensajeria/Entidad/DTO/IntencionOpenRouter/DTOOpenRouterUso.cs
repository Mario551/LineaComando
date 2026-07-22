using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterUso
{
    [JsonPropertyName("prompt_tokens")]
    public int? TokensPrompt { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? TokensRespuesta { get; set; }

    [JsonPropertyName("total_tokens")]
    public int? TokensTotales { get; set; }

    [JsonPropertyName("reasoning_tokens")]
    public int? TokensRazonamiento { get; set; }

    [JsonPropertyName("completion_tokens_details")]
    public JsonElement? DetallesTokensRespuesta { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
