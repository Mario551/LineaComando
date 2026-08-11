using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ContactAddressInfobipConfiguracion : IEntityTypeConfiguration<ContactAddressInfobip>
{
    public void Configure(EntityTypeBuilder<ContactAddressInfobip> builder)
    {
        builder.ToTable(
            "per_contact_addresses_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_contact_addresses_infobip_address_index",
                "address_index >= 0"));

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_contact_addresses_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdSharedContactsInfobip)
            .HasColumnName("record_id_shared_contacts_infobip")
            .IsRequired();

        builder.Property(entity => entity.AddressIndex)
            .HasColumnName("address_index")
            .IsRequired();

        builder.Property(entity => entity.Street)
            .HasColumnName("street");

        builder.Property(entity => entity.City)
            .HasColumnName("city");

        builder.Property(entity => entity.State)
            .HasColumnName("state");

        builder.Property(entity => entity.Zip)
            .HasColumnName("zip");

        builder.Property(entity => entity.Country)
            .HasColumnName("country");

        builder.Property(entity => entity.CountryCode)
            .HasColumnName("country_code");

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
            entity.AddressIndex
        })
            .IsUnique()
            .HasDatabaseName("per_uk_contact_addresses_infobip_parent_index");

        builder.HasOne(entity => entity.SharedContactInfobip)
            .WithMany(entity => entity.ContactAddressesInfobip)
            .HasForeignKey(entity => entity.RecordIdSharedContactsInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_contact_addresses_infobip_shared_contacts_infobip");
    }
}
