using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class ContactMessageInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdInboundMessagesInfobip { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual InboundMessageInfobip InboundMessageInfobip { get; set; } = null!;
    public virtual ICollection<SharedContactInfobip> SharedContactsInfobip { get; set; } = [];
}
