using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class MessageReferralInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdInboundMessagesInfobip { get; set; }
    public required string SourceType { get; set; }
    public string? SourceId { get; set; }
    public required string SourceUrl { get; set; }
    public string? Headline { get; set; }
    public string? Body { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? CtwaClickId { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual InboundMessageInfobip InboundMessageInfobip { get; set; } = null!;
}
