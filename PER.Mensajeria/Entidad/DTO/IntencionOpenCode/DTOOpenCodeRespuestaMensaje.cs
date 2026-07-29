using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeRespuestaMensaje
{
    [JsonPropertyName("info")]
    public DTOOpenCodeMensajeAsistente Informacion { get; set; } = new();

    [JsonPropertyName("parts")]
    public List<DTOOpenCodeParte> Partes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
