using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class PaymentConfirmationMessageInfobipConfiguracion
    : IEntityTypeConfiguration<PaymentConfirmationMessageInfobip>
{
    public void Configure(EntityTypeBuilder<PaymentConfirmationMessageInfobip> builder)
    {
        builder.ToTable("per_payment_confirmation_messages_infobip", table =>
        {
            table.HasCheckConstraint(
                "per_ck_payment_confirmation_messages_status",
                "status IN ('PENDING', 'FAILED', 'SUCCESS', 'CANCELED', 'UNKNOWN')");

            table.HasCheckConstraint(
                "per_ck_payment_confirmation_messages_currency",
                "currency IN ('INR', 'BRL', 'UNKNOWN')");

            table.HasCheckConstraint(
                "per_ck_payment_confirmation_messages_transaction_type",
                "transaction_type IN ('UPI', 'BR', 'UNKNOWN')");
        });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_payment_confirmation_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.ReferenceId)
            .HasColumnName("reference_id")
            .IsRequired();

        builder.Property(entity => entity.PaymentId)
            .HasColumnName("payment_id")
            .IsRequired(false);

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .IsRequired();

        builder.Property(entity => entity.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.Property(entity => entity.Offset)
            .HasColumnName("offset")
            .IsRequired();

        builder.Property(entity => entity.TransactionId)
            .HasColumnName("transaction_id")
            .IsRequired();

        builder.Property(entity => entity.TransactionType)
            .HasColumnName("transaction_type")
            .IsRequired();

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_payment_confirmation_messages_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.PaymentConfirmationMessageInfobip)
            .HasForeignKey<PaymentConfirmationMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_payment_confirmation_messages_inbound");
    }
}
