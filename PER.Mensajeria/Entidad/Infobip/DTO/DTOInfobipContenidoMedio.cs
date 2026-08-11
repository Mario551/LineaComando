using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContenidoMedio
{
    [JsonPropertyName("mediaUrl")]
    public string MediaUrl { get; set; } = string.Empty;

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}
