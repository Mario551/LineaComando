using System.Text.Json;
using System.Text.Json.Serialization;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Entidad.Infobip.JsonConverter;

public sealed class InfobipFlowResponseNodesJsonConverter
    : JsonConverter<List<DTOInfobipFlowResponseNode>>
{
    public override bool HandleNull => true;

    public override List<DTOInfobipFlowResponseNode> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("La respuesta de Flow debe ser un objeto.");
        }

        return ReadObjectChildren(ref reader);
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<DTOInfobipFlowResponseNode> value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (DTOInfobipFlowResponseNode node in value)
        {
            if (node.Key is null || node.ElementIndex is not null)
            {
                throw new JsonException("Cada nodo raíz de Flow debe tener una clave.");
            }

            writer.WritePropertyName(node.Key);
            WriteNodeValue(writer, node);
        }

        writer.WriteEndObject();
    }

    private static List<DTOInfobipFlowResponseNode> ReadObjectChildren(
        ref Utf8JsonReader reader)
    {
        List<DTOInfobipFlowResponseNode> children = [];

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return children;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Se esperaba una propiedad dentro del objeto Flow.");
            }

            string key = reader.GetString()
                ?? throw new JsonException("La propiedad Flow no contiene una clave válida.");

            if (!reader.Read())
            {
                throw new JsonException("La propiedad Flow no contiene un valor.");
            }

            children.Add(ReadNode(ref reader, key, null));
        }

        throw new JsonException("El objeto Flow no fue cerrado correctamente.");
    }

    private static List<DTOInfobipFlowResponseNode> ReadArrayChildren(
        ref Utf8JsonReader reader)
    {
        List<DTOInfobipFlowResponseNode> children = [];
        int elementIndex = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return children;
            }

            children.Add(ReadNode(ref reader, null, elementIndex));
            elementIndex++;
        }

        throw new JsonException("El arreglo Flow no fue cerrado correctamente.");
    }

    private static DTOInfobipFlowResponseNode ReadNode(
        ref Utf8JsonReader reader,
        string? key,
        int? elementIndex)
    {
        DTOInfobipFlowResponseNode node = new()
        {
            Key = key,
            ElementIndex = elementIndex,
            NodeType = GetNodeType(reader.TokenType)
        };

        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                node.Children = ReadObjectChildren(ref reader);
                break;
            case JsonTokenType.StartArray:
                node.Children = ReadArrayChildren(ref reader);
                break;
            case JsonTokenType.String:
                node.TextValue = reader.GetString();
                break;
            case JsonTokenType.Number:
                if (!reader.TryGetDecimal(out decimal numericValue))
                {
                    throw new JsonException(
                        "El número de Flow está fuera del rango decimal admitido.");
                }

                node.NumericValue = numericValue;
                break;
            case JsonTokenType.True:
            case JsonTokenType.False:
                node.BooleanValue = reader.GetBoolean();
                break;
            case JsonTokenType.Null:
                break;
            default:
                throw new JsonException("El tipo de valor Flow no está soportado.");
        }

        return node;
    }

    private static string GetNodeType(JsonTokenType tokenType)
    {
        return tokenType switch
        {
            JsonTokenType.StartObject => "OBJECT",
            JsonTokenType.StartArray => "ARRAY",
            JsonTokenType.String => "STRING",
            JsonTokenType.Number => "NUMBER",
            JsonTokenType.True => "BOOLEAN",
            JsonTokenType.False => "BOOLEAN",
            JsonTokenType.Null => "NULL",
            _ => throw new JsonException("El tipo de valor Flow no está soportado.")
        };
    }

    private static void WriteNodeValue(
        Utf8JsonWriter writer,
        DTOInfobipFlowResponseNode node)
    {
        switch (node.NodeType)
        {
            case "OBJECT":
                WriteObjectNode(writer, node);
                break;
            case "ARRAY":
                WriteArrayNode(writer, node);
                break;
            case "STRING":
                writer.WriteStringValue(
                    node.TextValue
                    ?? throw new JsonException("El nodo STRING requiere TextValue."));
                break;
            case "NUMBER":
                writer.WriteNumberValue(
                    node.NumericValue
                    ?? throw new JsonException("El nodo NUMBER requiere NumericValue."));
                break;
            case "BOOLEAN":
                writer.WriteBooleanValue(
                    node.BooleanValue
                    ?? throw new JsonException("El nodo BOOLEAN requiere BooleanValue."));
                break;
            case "NULL":
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException($"El tipo de nodo Flow '{node.NodeType}' no es válido.");
        }
    }

    private static void WriteObjectNode(
        Utf8JsonWriter writer,
        DTOInfobipFlowResponseNode node)
    {
        writer.WriteStartObject();

        foreach (DTOInfobipFlowResponseNode child in node.Children)
        {
            if (child.Key is null || child.ElementIndex is not null)
            {
                throw new JsonException(
                    "Cada hijo de un nodo OBJECT debe tener una clave.");
            }

            writer.WritePropertyName(child.Key);
            WriteNodeValue(writer, child);
        }

        writer.WriteEndObject();
    }

    private static void WriteArrayNode(
        Utf8JsonWriter writer,
        DTOInfobipFlowResponseNode node)
    {
        writer.WriteStartArray();

        for (int index = 0; index < node.Children.Count; index++)
        {
            DTOInfobipFlowResponseNode child = node.Children[index];

            if (child.Key is not null || child.ElementIndex != index)
            {
                throw new JsonException(
                    "Cada hijo de un nodo ARRAY debe tener un índice consecutivo.");
            }

            WriteNodeValue(writer, child);
        }

        writer.WriteEndArray();
    }
}
