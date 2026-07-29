using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeParteEntrada
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Texto { get; set; } = string.Empty;
}
