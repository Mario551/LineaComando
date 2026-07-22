using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

public class DTOOpenRouterFormatoRespuesta
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = "json_object";
}
