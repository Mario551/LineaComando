using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class SharedContactInfobipConfiguracion : IEntityTypeConfiguration<SharedContactInfobip>
{
    public void Configure(EntityTypeBuilder<SharedContactInfobip> builder)
    {
        builder.ToTable(
            "per_shared_contacts_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_shared_contacts_infobip_contact_index",
                "contact_index >= 0"));

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_shared_contacts_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdContactMessagesInfobip)
            .HasColumnName("record_id_contact_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.ContactIndex)
            .HasColumnName("contact_index")
            .IsRequired();

        builder.Property(entity => entity.Birthday)
            .HasColumnName("birthday");

        builder.Property(entity => entity.FirstName)
            .HasColumnName("first_name");

        builder.Property(entity => entity.LastName)
            .HasColumnName("last_name");

        builder.Property(entity => entity.MiddleName)
            .HasColumnName("middle_name");

        builder.Property(entity => entity.NameSuffix)
            .HasColumnName("name_suffix");

        builder.Property(entity => entity.NamePrefix)
            .HasColumnName("name_prefix");

        builder.Property(entity => entity.FormattedName)
            .HasColumnName("formatted_name");

        builder.Property(entity => entity.Company)
            .HasColumnName("company");

        builder.Property(entity => entity.Department)
            .HasColumnName("department");

        builder.Property(entity => entity.Title)
            .HasColumnName("title");

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => new
        {
            entity.RecordIdContactMessagesInfobip,
            entity.ContactIndex
        })
            .IsUnique()
            .HasDatabaseName("per_uk_shared_contacts_infobip_parent_index");

        builder.HasOne(entity => entity.ContactMessageInfobip)
            .WithMany(entity => entity.SharedContactsInfobip)
            .HasForeignKey(entity => entity.RecordIdContactMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_shared_contacts_infobip_contact_messages_infobip");
    }
}
