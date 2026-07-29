using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeParte
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("sessionID")]
    public string? IDSesion { get; set; }

    [JsonPropertyName("messageID")]
    public string? IDMensaje { get; set; }

    [JsonPropertyName("type")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Texto { get; set; }

    [JsonPropertyName("reason")]
    public string? Razon { get; set; }

    [JsonPropertyName("synthetic")]
    public bool? Sintetica { get; set; }

    [JsonPropertyName("ignored")]
    public bool? Ignorada { get; set; }

    [JsonPropertyName("cost")]
    public decimal? Costo { get; set; }

    [JsonPropertyName("tokens")]
    public DTOOpenCodeTokens? Tokens { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? DatosAdicionales { get; set; }
}
