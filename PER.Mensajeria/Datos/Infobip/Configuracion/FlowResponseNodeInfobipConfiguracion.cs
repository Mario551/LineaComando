using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class FlowResponseNodeInfobipConfiguracion
    : IEntityTypeConfiguration<FlowResponseNodeInfobip>
{
    public void Configure(EntityTypeBuilder<FlowResponseNodeInfobip> builder)
    {
        builder.ToTable("per_flow_response_nodes_infobip", table =>
        {
            table.HasCheckConstraint(
                "per_ck_flow_response_nodes_node_type",
                "node_type IN ('OBJECT', 'ARRAY', 'STRING', 'NUMBER', 'BOOLEAN', 'NULL')");

            table.HasCheckConstraint(
                "per_ck_flow_response_nodes_key_or_index",
                "((key IS NOT NULL AND element_index IS NULL) OR " +
                "(key IS NULL AND element_index IS NOT NULL))");

            table.HasCheckConstraint(
                "per_ck_flow_response_nodes_element_index",
                "element_index IS NULL OR element_index >= 0");

            table.HasCheckConstraint(
                "per_ck_flow_response_nodes_root_key",
                "record_id_flow_response_nodes_infobip_parent IS NOT NULL OR key IS NOT NULL");

            table.HasCheckConstraint(
                "per_ck_flow_response_nodes_typed_value",
                "((node_type IN ('OBJECT', 'ARRAY', 'NULL') " +
                "AND text_value IS NULL AND numeric_value IS NULL AND boolean_value IS NULL) OR " +
                "(node_type = 'STRING' " +
                "AND text_value IS NOT NULL AND numeric_value IS NULL AND boolean_value IS NULL) OR " +
                "(node_type = 'NUMBER' " +
                "AND text_value IS NULL AND numeric_value IS NOT NULL AND boolean_value IS NULL) OR " +
                "(node_type = 'BOOLEAN' " +
                "AND text_value IS NULL AND numeric_value IS NULL AND boolean_value IS NOT NULL))");
        });

        builder.HasKey(entity => entity.RecordId)
            .HasName("per_pk_flow_response_nodes_infobip");

        builder.HasAlternateKey(entity => new
        {
            entity.RecordIdFlowReplyMessagesInfobip,
            entity.RecordId
        })
            .HasName("per_uk_flow_response_nodes_owner_record");

        builder.Property(entity => entity.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.RecordIdFlowReplyMessagesInfobip)
            .HasColumnName("record_id_flow_reply_messages_infobip")
            .IsRequired();

        builder.Property(entity => entity.RecordIdFlowResponseNodesInfobipParent)
            .HasColumnName("record_id_flow_response_nodes_infobip_parent")
            .IsRequired(false);

        builder.Property(entity => entity.Key)
            .HasColumnName("key")
            .IsRequired(false);

        builder.Property(entity => entity.ElementIndex)
            .HasColumnName("element_index")
            .IsRequired(false);

        builder.Property(entity => entity.NodeType)
            .HasColumnName("node_type")
            .IsRequired();

        builder.Property(entity => entity.TextValue)
            .HasColumnName("text_value")
            .IsRequired(false);

        builder.Property(entity => entity.NumericValue)
            .HasColumnName("numeric_value")
            .HasPrecision(38, 18)
            .IsRequired(false);

        builder.Property(entity => entity.BooleanValue)
            .HasColumnName("boolean_value")
            .IsRequired(false);

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at")
            .IsRequired(false);

        builder.HasIndex(entity => new
        {
            entity.RecordIdFlowReplyMessagesInfobip,
            entity.Key
        })
            .IsUnique()
            .HasFilter(
                "record_id_flow_response_nodes_infobip_parent IS NULL " +
                "AND key IS NOT NULL")
            .HasDatabaseName("per_uk_flow_response_nodes_root_key");

        builder.HasIndex(entity => new
        {
            entity.RecordIdFlowReplyMessagesInfobip,
            entity.RecordIdFlowResponseNodesInfobipParent,
            entity.Key
        })
            .IsUnique()
            .HasFilter(
                "record_id_flow_response_nodes_infobip_parent IS NOT NULL " +
                "AND key IS NOT NULL")
            .HasDatabaseName("per_uk_flow_response_nodes_sibling_key");

        builder.HasIndex(entity => new
        {
            entity.RecordIdFlowReplyMessagesInfobip,
            entity.RecordIdFlowResponseNodesInfobipParent,
            entity.ElementIndex
        })
            .IsUnique()
            .HasFilter(
                "record_id_flow_response_nodes_infobip_parent IS NOT NULL " +
                "AND element_index IS NOT NULL")
            .HasDatabaseName("per_uk_flow_response_nodes_sibling_index");

        builder.HasOne(entity => entity.FlowReplyMessageInfobip)
            .WithMany(entity => entity.FlowResponseNodesInfobip)
            .HasForeignKey(entity => entity.RecordIdFlowReplyMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_flow_response_nodes_flow_reply_message");

        builder.HasOne(entity => entity.Parent)
            .WithMany(entity => entity.Children)
            .HasForeignKey(entity => new
            {
                entity.RecordIdFlowReplyMessagesInfobip,
                entity.RecordIdFlowResponseNodesInfobipParent
            })
            .HasPrincipalKey(entity => new
            {
                entity.RecordIdFlowReplyMessagesInfobip,
                entity.RecordId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("per_fk_flow_response_nodes_parent");
    }
}
