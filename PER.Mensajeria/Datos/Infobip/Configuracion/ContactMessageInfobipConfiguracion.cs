using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ContactMessageInfobipConfiguracion : IEntityTypeConfiguration<ContactMessageInfobip>
{
    public void Configure(EntityTypeBuilder<ContactMessageInfobip> builder)
    {
        builder.ToTable("per_contact_messages_infobip");

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_contact_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_contact_messages_infobip_inbound_message");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.ContactMessageInfobip)
            .HasForeignKey<ContactMessageInfobip>(entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_contact_messages_infobip_inbound_messages_infobip");
    }
}
