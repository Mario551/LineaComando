using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipContenidoTexto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
