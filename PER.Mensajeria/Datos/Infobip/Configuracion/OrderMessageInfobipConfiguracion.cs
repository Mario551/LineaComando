using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class OrderMessageInfobipConfiguracion
    : IEntityTypeConfiguration<OrderMessageInfobip>
{
    public void Configure(EntityTypeBuilder<OrderMessageInfobip> builder)
    {
        builder.ToTable("per_order_messages_infobip");

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_order_messages_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.CatalogId)
            .HasColumnName("catalog_id")
            .IsRequired();

        builder.Property(entity => entity.Text)
            .HasColumnName("text")
            .IsRequired(false);

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_order_messages_inbound");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.OrderMessageInfobip)
            .HasForeignKey<OrderMessageInfobip>(
                entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_order_messages_inbound");
    }
}
