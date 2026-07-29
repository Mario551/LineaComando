using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeMensajeAsistente
{
    [JsonPropertyName("id")]
    public string ID { get; set; } = string.Empty;

    [JsonPropertyName("sessionID")]
    public string IDSesion { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public DTOOpenCodeTiempoMensaje? Tiempo { get; set; }

    [JsonPropertyName("error")]
    public DTOOpenCodeError? Error { get; set; }

    [JsonPropertyName("parentID")]
    public string? IDPadre { get; set; }

    [JsonPropertyName("modelID")]
    public string? IDModelo { get; set; }

    [JsonPropertyName("providerID")]
    public string? IDProveedor { get; set; }

    [JsonPropertyName("mode")]
    public string? Modo { get; set; }

    [JsonPropertyName("cost")]
    public decimal? Costo { get; set; }

    [JsonPropertyName("tokens")]
    public DTOOpenCodeTokens? Tokens { get; set; }

    [JsonPropertyName("finish")]
    public string? RazonFinalizacion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
