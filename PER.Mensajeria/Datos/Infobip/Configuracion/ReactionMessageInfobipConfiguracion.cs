using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ReactionMessageInfobipConfiguracion
    : IEntityTypeConfiguration<ReactionMessageInfobip>
{
    public void Configure(EntityTypeBuilder<ReactionMessageInfobip> builder)
    {
        builder.ToTable("per_reaction_messages_infobip", table =>
        {
            table.HasCheckConstraint(
                "per_ck_reaction_messages_action",
                "action IS NULL OR action IN ('ADDED', 'REMOVED')");
        });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_reaction_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.Emoji)
            .HasColumnName("emoji")
            .IsRequired(false);

        builder.Property(entity => entity.Action)
            .HasColumnName("action")
            .IsRequired(false);

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_reaction_messages_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.ReactionMessageInfobip)
            .HasForeignKey<ReactionMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_reaction_messages_inbound");
    }
}
