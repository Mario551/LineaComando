using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterRespuestaChat
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("model")]
    public string? Modelo { get; set; }

    [JsonPropertyName("provider")]
    public string? Proveedor { get; set; }

    [JsonPropertyName("choices")]
    public List<DTOOpenRouterEleccion> Elecciones { get; set; } = [];

    [JsonPropertyName("usage")]
    public DTOOpenRouterUso? Uso { get; set; }

    [JsonPropertyName("error")]
    public DTOOpenRouterError? Error { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
