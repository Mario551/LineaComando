using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeTiempoMensaje
{
    [JsonPropertyName("created")]
    public long Creado { get; set; }

    [JsonPropertyName("completed")]
    public long? Completado { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
