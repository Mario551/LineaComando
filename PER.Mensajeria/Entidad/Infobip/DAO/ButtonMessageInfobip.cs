using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class ButtonMessageInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdInboundMessagesInfobip { get; set; }
    public required string Text { get; set; }
    public required string Payload { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual InboundMessageInfobip InboundMessageInfobip { get; set; } = null!;
}
