using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class ContactPhoneInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdSharedContactsInfobip { get; set; }
    public int PhoneIndex { get; set; }
    public string? Phone { get; set; }
    public string? Type { get; set; }
    public string? WaId { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual SharedContactInfobip SharedContactInfobip { get; set; } = null!;
}
