using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class ContactAddressInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdSharedContactsInfobip { get; set; }
    public int AddressIndex { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? Type { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual SharedContactInfobip SharedContactInfobip { get; set; } = null!;
}
