using PER.Mensajeria.Entidad.Infobip.JsonConverter;
using System.Text.Json.Serialization;

namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipMessage
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("contacts")]
    public List<DTOInfobipSharedContact>? Contacts { get; set; }

    [JsonPropertyName("malware")]
    public string? Malware { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("response")]
    [JsonConverter(typeof(InfobipFlowResponseNodesJsonConverter))]
    public List<DTOInfobipFlowResponseNode> Response { get; set; } = [];

    [JsonPropertyName("referenceId")]
    public string? ReferenceId { get; set; }

    [JsonPropertyName("paymentId")]
    public string? PaymentId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("totalAmount")]
    public DTOInfobipPaymentAmount? TotalAmount { get; set; }

    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("transactionType")]
    public string? TransactionType { get; set; }

    [JsonPropertyName("callPermissionReply")]
    public DTOInfobipCallPermissionReply? CallPermissionReply { get; set; }

    [JsonPropertyName("inThreadAuthenticationReply")]
    public DTOInfobipInThreadAuthenticationReply? InThreadAuthenticationReply { get; set; }

    [JsonPropertyName("catalogId")]
    public string? CatalogId { get; set; }

    [JsonPropertyName("productItems")]
    public List<DTOInfobipOrderProductItem>? ProductItems { get; set; }

    [JsonPropertyName("emoji")]
    public string? Emoji { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("context")]
    public DTOInfobipContext? Context { get; set; }

    [JsonPropertyName("referral")]
    public DTOInfobipReferral? Referral { get; set; }
}
