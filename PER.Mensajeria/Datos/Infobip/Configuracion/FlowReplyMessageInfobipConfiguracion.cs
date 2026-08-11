using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class FlowReplyMessageInfobipConfiguracion
    : IEntityTypeConfiguration<FlowReplyMessageInfobip>
{
    public void Configure(EntityTypeBuilder<FlowReplyMessageInfobip> builder)
    {
        builder.ToTable("per_flow_reply_messages_infobip");

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_flow_reply_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.Text)
            .HasColumnName("text")
            .IsRequired();

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_flow_reply_messages_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.FlowReplyMessageInfobip)
            .HasForeignKey<FlowReplyMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_flow_reply_messages_inbound");
    }
}
