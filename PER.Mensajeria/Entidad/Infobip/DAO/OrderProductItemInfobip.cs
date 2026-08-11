using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class OrderProductItemInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdOrderMessagesInfobip { get; set; }
    public int ProductItemIndex { get; set; }
    public required string Currency { get; set; }
    public decimal ItemPrice { get; set; }
    public required string ProductRetailerId { get; set; }
    public int Quantity { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual OrderMessageInfobip OrderMessageInfobip { get; set; } = null!;
}
