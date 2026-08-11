using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ContactUrlInfobipConfiguracion : IEntityTypeConfiguration<ContactUrlInfobip>
{
    public void Configure(EntityTypeBuilder<ContactUrlInfobip> builder)
    {
        builder.ToTable(
            "per_contact_urls_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_contact_urls_infobip_url_index",
                "url_index >= 0"));

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_contact_urls_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdSharedContactsInfobip)
            .HasColumnName("record_id_shared_contacts_infobip")
            .IsRequired();

        builder.Property(entity => entity.UrlIndex)
            .HasColumnName("url_index")
            .IsRequired();

        builder.Property(entity => entity.Url)
            .HasColumnName("url");

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
            entity.UrlIndex
        })
            .IsUnique()
            .HasDatabaseName("per_uk_contact_urls_infobip_parent_index");

        builder.HasOne(entity => entity.SharedContactInfobip)
            .WithMany(entity => entity.ContactUrlsInfobip)
            .HasForeignKey(entity => entity.RecordIdSharedContactsInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_contact_urls_infobip_shared_contacts_infobip");
    }
}
