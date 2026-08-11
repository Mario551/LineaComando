using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ContactPhoneInfobipConfiguracion : IEntityTypeConfiguration<ContactPhoneInfobip>
{
    public void Configure(EntityTypeBuilder<ContactPhoneInfobip> builder)
    {
        builder.ToTable(
            "per_contact_phones_infobip",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "per_ck_contact_phones_infobip_phone_index",
                    "phone_index >= 0");
                tableBuilder.HasCheckConstraint(
                    "per_ck_contact_phones_infobip_type",
                    "type IS NULL OR type IN ('CELL', 'MAIN', 'IPHONE', 'HOME', 'WORK')");
            });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_contact_phones_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdSharedContactsInfobip)
            .HasColumnName("record_id_shared_contacts_infobip")
            .IsRequired();

        builder.Property(entity => entity.PhoneIndex)
            .HasColumnName("phone_index")
            .IsRequired();

        builder.Property(entity => entity.Phone)
            .HasColumnName("phone");

        builder.Property(entity => entity.Type)
            .HasColumnName("type");

        builder.Property(entity => entity.WaId)
            .HasColumnName("wa_id");

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => new
        {
            entity.RecordIdSharedContactsInfobip,
            entity.PhoneIndex
        })
            .IsUnique()
            .HasDatabaseName("per_uk_contact_phones_infobip_parent_index");

        builder.HasOne(entity => entity.SharedContactInfobip)
            .WithMany(entity => entity.ContactPhonesInfobip)
            .HasForeignKey(entity => entity.RecordIdSharedContactsInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_contact_phones_infobip_shared_contacts_infobip");
    }
}
