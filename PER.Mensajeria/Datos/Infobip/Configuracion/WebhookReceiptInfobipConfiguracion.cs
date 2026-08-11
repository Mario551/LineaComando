using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class WebhookReceiptInfobipConfiguracion : IEntityTypeConfiguration<WebhookReceiptInfobip>
{
    public void Configure(EntityTypeBuilder<WebhookReceiptInfobip> builder)
    {
        builder.ToTable(
            "per_webhook_receipts_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_webhook_receipts_infobip_identity_all_or_none",
                "(acknowledged IS NULL AND hash IS NULL AND created_at IS NULL) OR " +
                "(acknowledged IS NOT NULL AND hash IS NOT NULL AND created_at IS NOT NULL)"));

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_webhook_receipts_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(255);

        builder.Property(entity => entity.ApplicationId)
            .HasColumnName("application_id")
            .HasMaxLength(255);

        builder.Property(entity => entity.From)
            .HasColumnName("from")
            .IsRequired();

        builder.Property(entity => entity.To)
            .HasColumnName("to")
            .IsRequired();

        builder.Property(entity => entity.IntegrationType)
            .HasColumnName("integration_type")
            .IsRequired();

        builder.Property(entity => entity.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();

        builder.Property(entity => entity.Keyword)
            .HasColumnName("keyword");

        builder.Property(entity => entity.MessageId)
            .HasColumnName("message_id")
            .IsRequired();

        builder.Property(entity => entity.PairedMessageId)
            .HasColumnName("paired_message_id");

        builder.Property(entity => entity.CallbackData)
            .HasColumnName("callback_data");

        builder.Property(entity => entity.PricePerMessage)
            .HasColumnName("price_per_message")
            .HasPrecision(38, 18);

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency");

        builder.Property(entity => entity.Name)
            .HasColumnName("name");

        builder.Property(entity => entity.PhoneNumber)
            .HasColumnName("phone_number");

        builder.Property(entity => entity.UserId)
            .HasColumnName("user_id");

        builder.Property(entity => entity.ParentUserId)
            .HasColumnName("parent_user_id");

        builder.Property(entity => entity.Username)
            .HasColumnName("username");

        builder.Property(entity => entity.Acknowledged)
            .HasColumnName("acknowledged");

        builder.Property(entity => entity.Hash)
            .HasColumnName("hash");

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => entity.MessageId)
            .IsUnique()
            .HasDatabaseName("per_uk_webhook_receipts_infobip_message_id");
    }
}
