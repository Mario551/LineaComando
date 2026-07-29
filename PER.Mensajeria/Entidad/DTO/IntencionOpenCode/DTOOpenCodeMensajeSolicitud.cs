using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeMensajeSolicitud
{
    [JsonPropertyName("agent")]
    public string Agente { get; set; } = string.Empty;

    [JsonPropertyName("system")]
    public string Sistema { get; set; } = string.Empty;

    [JsonPropertyName("tools")]
    public Dictionary<string, bool> Herramientas { get; set; } = [];

    [JsonPropertyName("parts")]
    public List<DTOOpenCodeParteEntrada> Partes { get; set; } = [];
}
