using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class InThreadAuthenticationReplyMessageInfobipConfiguracion
    : IEntityTypeConfiguration<InThreadAuthenticationReplyMessageInfobip>
{
    public void Configure(EntityTypeBuilder<InThreadAuthenticationReplyMessageInfobip> builder)
    {
        builder.ToTable("per_in_thread_authentication_reply_messages_infobip", table =>
        {
            table.HasCheckConstraint(
                "per_ck_in_thread_authentication_reply_status",
                "status IN ('UNSUPPORTED', 'VERIFIED', 'INTERACTION_CANCELED', " +
                "'VERIFICATION_FAILED', 'UNKNOWN')");
        });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_in_thread_authentication_reply_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(entity => entity.BusinessScopedPasskeyHash)
            .HasColumnName("business_scoped_passkey_hash")
            .IsRequired(false);

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_in_thread_authentication_reply_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.InThreadAuthenticationReplyMessageInfobip)
            .HasForeignKey<InThreadAuthenticationReplyMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_in_thread_authentication_reply_inbound");
    }
}
