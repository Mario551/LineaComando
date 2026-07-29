using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

public class DTOOpenCodeCrearSesionSolicitud
{
    [JsonPropertyName("title")]
    public string Titulo { get; set; } = string.Empty;
}
