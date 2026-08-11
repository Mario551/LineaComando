using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class PaymentConfirmationMessageInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdInboundMessagesInfobip { get; set; }
    public required string ReferenceId { get; set; }
    public string? PaymentId { get; set; }
    public required string Status { get; set; }
    public required string Currency { get; set; }
    public int Value { get; set; }
    public int Offset { get; set; }
    public required string TransactionId { get; set; }
    public required string TransactionType { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual InboundMessageInfobip InboundMessageInfobip { get; set; } = null!;
}
