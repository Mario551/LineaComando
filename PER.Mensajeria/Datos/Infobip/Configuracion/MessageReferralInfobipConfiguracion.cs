using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class MessageReferralInfobipConfiguracion : IEntityTypeConfiguration<MessageReferralInfobip>
{
    public void Configure(EntityTypeBuilder<MessageReferralInfobip> builder)
    {
        builder.ToTable(
            "per_message_referrals_infobip",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "per_ck_message_referrals_infobip_source_type",
                    "source_type IN ('AD', 'POST', 'UNKNOWN')");
                tableBuilder.HasCheckConstraint(
                    "per_ck_message_referrals_infobip_media_all_or_none",
                    "(type IS NULL AND url IS NULL) OR (type IS NOT NULL AND url IS NOT NULL)");
                tableBuilder.HasCheckConstraint(
                    "per_ck_message_referrals_infobip_media_type",
                    "type IS NULL OR type IN ('IMAGE', 'VIDEO')");
            });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_message_referrals_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.SourceType)
            .HasColumnName("source_type")
            .IsRequired();

        builder.Property(entity => entity.SourceId)
            .HasColumnName("source_id");

        builder.Property(entity => entity.SourceUrl)
            .HasColumnName("source_url")
            .IsRequired();

        builder.Property(entity => entity.Headline)
            .HasColumnName("headline");

        builder.Property(entity => entity.Body)
            .HasColumnName("body");

        builder.Property(entity => entity.Type)
            .HasColumnName("type");

        builder.Property(entity => entity.Url)
            .HasColumnName("url");

        builder.Property(entity => entity.CtwaClickId)
            .HasColumnName("ctwa_click_id");

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_message_referrals_infobip_inbound_message");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.MessageReferralInfobip)
            .HasForeignKey<MessageReferralInfobip>(entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_message_referrals_infobip_inbound_messages_infobip");
    }
}
