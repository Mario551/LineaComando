using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ButtonMessageInfobipConfiguracion : IEntityTypeConfiguration<ButtonMessageInfobip>
{
    public void Configure(EntityTypeBuilder<ButtonMessageInfobip> builder)
    {
        builder.ToTable("per_button_messages_infobip");

        builder.HasKey(message => message.RecordId);

        builder.Property(message => message.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(message => message.Text)
            .HasColumnName("text")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(message => message.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(message => message.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasOne(message => message.InboundMessageInfobip)
            .WithOne(message => message.ButtonMessageInfobip)
            .HasForeignKey<ButtonMessageInfobip>(message => message.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
