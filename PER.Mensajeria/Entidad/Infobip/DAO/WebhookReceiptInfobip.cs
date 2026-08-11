using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class WebhookReceiptInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public string? EntityId { get; set; }
    public string? ApplicationId { get; set; }
    public required string From { get; set; }
    public required string To { get; set; }
    public required string IntegrationType { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string? Keyword { get; set; }
    public required string MessageId { get; set; }
    public string? PairedMessageId { get; set; }
    public string? CallbackData { get; set; }
    public decimal? PricePerMessage { get; set; }
    public string? Currency { get; set; }
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UserId { get; set; }
    public string? ParentUserId { get; set; }
    public string? Username { get; set; }
    public bool? Acknowledged { get; set; }
    public string? Hash { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual InboundMessageInfobip InboundMessageInfobip { get; set; } = null!;
    public virtual DAOProcesamientoMensajeEntranteInfobip ProcesamientoMensajeEntranteInfobip { get; set; } = null!;
}
