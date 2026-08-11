using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class MessageContextInfobipConfiguracion : IEntityTypeConfiguration<MessageContextInfobip>
{
    public void Configure(EntityTypeBuilder<MessageContextInfobip> builder)
    {
        builder.ToTable(
            "per_message_contexts_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_message_contexts_infobip_referred_product_all_or_none",
                "(catalog_id IS NULL AND product_retailer_id IS NULL) OR " +
                "(catalog_id IS NOT NULL AND product_retailer_id IS NOT NULL)"));

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_message_contexts_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.From)
            .HasColumnName("from");

        builder.Property(entity => entity.Id)
            .HasColumnName("id");

        builder.Property(entity => entity.GroupId)
            .HasColumnName("group_id");

        builder.Property(entity => entity.CatalogId)
            .HasColumnName("catalog_id");

        builder.Property(entity => entity.ProductRetailerId)
            .HasColumnName("product_retailer_id");

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasIndex(entity => entity.RecordIdInboundMessagesInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_message_contexts_infobip_inbound_message");

        builder.HasOne(entity => entity.InboundMessageInfobip)
            .WithOne(entity => entity.MessageContextInfobip)
            .HasForeignKey<MessageContextInfobip>(entity => entity.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_message_contexts_infobip_inbound_messages_infobip");
    }
}
