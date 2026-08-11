using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class InteractiveListReplyMessageInfobipConfiguracion
    : IEntityTypeConfiguration<InteractiveListReplyMessageInfobip>
{
    public void Configure(EntityTypeBuilder<InteractiveListReplyMessageInfobip> builder)
    {
        builder.ToTable("per_interactive_list_reply_messages_infobip");

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_interactive_list_reply_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.Id)
            .HasColumnName("id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.Title)
            .HasColumnName("title")
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasMaxLength(72)
            .IsRequired(false);

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_interactive_list_reply_messages_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.InteractiveListReplyMessageInfobip)
            .HasForeignKey<InteractiveListReplyMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_interactive_list_reply_messages_inbound");
    }
}
