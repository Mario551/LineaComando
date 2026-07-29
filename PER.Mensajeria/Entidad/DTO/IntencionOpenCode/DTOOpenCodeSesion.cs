using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeSesion
{
    [JsonPropertyName("id")]
    public string ID { get; set; } = string.Empty;

    [JsonPropertyName("projectID")]
    public string? IDProyecto { get; set; }

    [JsonPropertyName("directory")]
    public string? DirectorioServidor { get; set; }

    [JsonPropertyName("title")]
    public string? Titulo { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
