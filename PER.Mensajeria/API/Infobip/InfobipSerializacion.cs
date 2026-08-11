using System.Text.Json;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.API.Infobip;

internal static class InfobipSerializacion
{
    public static JsonSerializerOptions Opciones { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}
