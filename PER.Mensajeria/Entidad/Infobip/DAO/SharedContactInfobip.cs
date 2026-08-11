using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class SharedContactInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdContactMessagesInfobip { get; set; }
    public int ContactIndex { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string? NameSuffix { get; set; }
    public string? NamePrefix { get; set; }
    public string? FormattedName { get; set; }
    public string? Company { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual ContactMessageInfobip ContactMessageInfobip { get; set; } = null!;
    public virtual ICollection<ContactAddressInfobip> ContactAddressesInfobip { get; set; } = [];
    public virtual ICollection<ContactEmailInfobip> ContactEmailsInfobip { get; set; } = [];
    public virtual ICollection<ContactPhoneInfobip> ContactPhonesInfobip { get; set; } = [];
    public virtual ICollection<ContactUrlInfobip> ContactUrlsInfobip { get; set; } = [];
}
