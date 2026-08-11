using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ContactEmailInfobipConfiguracion : IEntityTypeConfiguration<ContactEmailInfobip>
{
    public void Configure(EntityTypeBuilder<ContactEmailInfobip> builder)
    {
        builder.ToTable(
            "per_contact_emails_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_contact_emails_infobip_email_index",
                "email_index >= 0"));

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_contact_emails_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdSharedContactsInfobip)
            .HasColumnName("record_id_shared_contacts_infobip")
            .IsRequired();

        builder.Property(entity => entity.EmailIndex)
            .HasColumnName("email_index")
            .IsRequired();

        builder.Property(entity => entity.Email)
            .HasColumnName("email");

        builder.Property(entity => entity.Type)
            .HasColumnName("type");

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => new
        {
            entity.RecordIdSharedContactsInfobip,
            entity.EmailIndex
        })
            .IsUnique()
            .HasDatabaseName("per_uk_contact_emails_infobip_parent_index");

        builder.HasOne(entity => entity.SharedContactInfobip)
            .WithMany(entity => entity.ContactEmailsInfobip)
            .HasForeignKey(entity => entity.RecordIdSharedContactsInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_contact_emails_infobip_shared_contacts_infobip");
    }
}
