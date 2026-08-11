using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class MessageTypeInfobip : IAuditableEntity
{
    public required string Type { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual ICollection<InboundMessageInfobip> InboundMessagesInfobip { get; set; } = [];
}
