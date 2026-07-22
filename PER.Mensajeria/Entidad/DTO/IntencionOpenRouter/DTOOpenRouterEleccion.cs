using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterEleccion
{
    [JsonPropertyName("index")]
    public int Indice { get; set; }

    [JsonPropertyName("message")]
    public DTOOpenRouterMensaje Mensaje { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? RazonFinalizacion { get; set; }

    [JsonPropertyName("native_finish_reason")]
    public string? RazonFinalizacionNativa { get; set; }

    [JsonPropertyName("error")]
    public DTOOpenRouterError? Error { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? PropiedadesAdicionales { get; set; }
}
