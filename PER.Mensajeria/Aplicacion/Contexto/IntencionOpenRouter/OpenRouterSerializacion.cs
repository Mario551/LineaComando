using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

internal static class OpenRouterSerializacion
{
    public static JsonSerializerOptions Opciones { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}
