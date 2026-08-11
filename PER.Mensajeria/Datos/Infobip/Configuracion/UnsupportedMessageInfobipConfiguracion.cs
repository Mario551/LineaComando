using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class UnsupportedMessageInfobipConfiguracion : IEntityTypeConfiguration<UnsupportedMessageInfobip>
{
    public void Configure(EntityTypeBuilder<UnsupportedMessageInfobip> builder)
    {
        builder.ToTable("per_unsupported_messages_infobip");

        builder.HasKey(message => message.RecordId);

        builder.Property(message => message.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(message => message.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(message => message.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasOne(message => message.InboundMessageInfobip)
            .WithOne(message => message.UnsupportedMessageInfobip)
            .HasForeignKey<UnsupportedMessageInfobip>(message => message.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
