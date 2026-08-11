namespace PER.Mensajeria.Entidad.Infobip.Interfaz;

public interface IAuditableEntity
{
    DateTime RecordCreatedAt { get; set; }
    DateTime? RecordUpdatedAt { get; set; }
}
