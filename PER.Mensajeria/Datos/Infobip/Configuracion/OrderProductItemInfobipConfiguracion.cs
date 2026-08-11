using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class OrderProductItemInfobipConfiguracion
    : IEntityTypeConfiguration<OrderProductItemInfobip>
{
    public void Configure(EntityTypeBuilder<OrderProductItemInfobip> builder)
    {
        builder.ToTable("per_order_product_items_infobip", table =>
        {
            table.HasCheckConstraint(
                "per_ck_order_product_items_product_item_index",
                "product_item_index >= 0");

            table.HasCheckConstraint(
                "per_ck_order_product_items_quantity",
                "quantity >= 1");
        });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_order_product_items_infobip");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdOrderMessagesInfobip)
            .HasColumnName("record_id_order_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.ProductItemIndex)
            .HasColumnName("product_item_index")
            .IsRequired();

        builder.Property(entity => entity.Currency)
            .HasColumnName("currency")
            .IsRequired();

        builder.Property(entity => entity.ItemPrice)
            .HasColumnName("item_price")
            .HasPrecision(38, 18)
            .IsRequired();

        builder.Property(entity => entity.ProductRetailerId)
            .HasColumnName("product_retailer_id")
            .IsRequired();

        builder.Property(entity => entity.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => new
        {
            entity.RecordIdOrderMessagesInfobip,
            entity.ProductItemIndex
        })
            .IsUnique()
            .HasDatabaseName("per_uk_order_product_items_parent_index");

        builder.HasOne(entity => entity.OrderMessageInfobip)
            .WithMany(entity => entity.OrderProductItemsInfobip)
            .HasForeignKey(entity => entity.RecordIdOrderMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_order_product_items_order_message");
    }
}
