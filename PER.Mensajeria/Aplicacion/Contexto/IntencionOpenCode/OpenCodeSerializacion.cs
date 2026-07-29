using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

internal static class OpenCodeSerializacion
{
    public static JsonSerializerOptions Opciones { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}
