using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipResult
{
    [JsonPropertyName("entityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("applicationId")]
    public string? ApplicationId { get; set; }

    [JsonPropertyName("from")]
    public required string From { get; set; }

    [JsonPropertyName("to")]
    public required string To { get; set; }

    [JsonPropertyName("integrationType")]
    public required string IntegrationType { get; set; }

    [JsonPropertyName("receivedAt")]
    public required string ReceivedAt { get; set; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }

    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    [JsonPropertyName("pairedMessageId")]
    public string? PairedMessageId { get; set; }

    [JsonPropertyName("callbackData")]
    public string? CallbackData { get; set; }

    [JsonPropertyName("message")]
    public required DTOInfobipMessage Message { get; set; }

    [JsonPropertyName("price")]
    public required DTOInfobipMessagePrice Price { get; set; }

    [JsonPropertyName("contact")]
    public required DTOInfobipContactProfile Contact { get; set; }

    [JsonPropertyName("identity")]
    public DTOInfobipIdentity? Identity { get; set; }
}
