using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class ContactEmailInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdSharedContactsInfobip { get; set; }
    public int EmailIndex { get; set; }
    public string? Email { get; set; }
    public string? Type { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual SharedContactInfobip SharedContactInfobip { get; set; } = null!;
}
