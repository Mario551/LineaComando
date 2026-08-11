using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class CallPermissionReplyMessageInfobipConfiguracion
    : IEntityTypeConfiguration<CallPermissionReplyMessageInfobip>
{
    public void Configure(EntityTypeBuilder<CallPermissionReplyMessageInfobip> builder)
    {
        builder.ToTable("per_call_permission_reply_messages_infobip", table =>
        {
            table.HasCheckConstraint(
                "per_ck_call_permission_reply_messages_response",
                "response IN ('ACCEPT', 'REJECT')");
        });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_call_permission_reply_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.Response)
            .HasColumnName("response")
            .IsRequired();

        builder.Property(entity => entity.ExpirationTimestamp)
            .HasColumnName("expiration_timestamp")
            .IsRequired(false);

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_call_permission_reply_messages_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.CallPermissionReplyMessageInfobip)
            .HasForeignKey<CallPermissionReplyMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_call_permission_reply_messages_inbound");
    }
}
