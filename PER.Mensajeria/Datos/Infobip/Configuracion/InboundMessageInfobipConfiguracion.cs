using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class InboundMessageInfobipConfiguracion : IEntityTypeConfiguration<InboundMessageInfobip>
{
    public void Configure(EntityTypeBuilder<InboundMessageInfobip> builder)
    {
        builder.ToTable("per_inbound_messages_infobip");

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_inbound_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdWebhookReceiptsInfobip)
            .HasColumnName("record_id_webhook_receipts_infobip")
            .IsRequired();

        builder.Property(entity => entity.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => entity.RecordIdWebhookReceiptsInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_inbound_messages_infobip_webhook_receipt");

        builder.HasOne(entity => entity.WebhookReceiptInfobip)
            .WithOne(entity => entity.InboundMessageInfobip)
            .HasForeignKey<InboundMessageInfobip>(entity => entity.RecordIdWebhookReceiptsInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_inbound_messages_infobip_webhook_receipts_infobip");

        builder.HasOne(entity => entity.MessageTypeInfobip)
            .WithMany(entity => entity.InboundMessagesInfobip)
            .HasForeignKey(entity => entity.Type)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("per_fk_inbound_messages_infobip_message_types_infobip");
    }
}
