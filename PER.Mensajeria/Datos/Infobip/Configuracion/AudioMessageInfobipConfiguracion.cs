using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class AudioMessageInfobipConfiguracion : IEntityTypeConfiguration<AudioMessageInfobip>
{
    public void Configure(EntityTypeBuilder<AudioMessageInfobip> builder)
    {
        builder.ToTable("per_audio_messages_infobip");

        builder.HasKey(message => message.RecordId);

        builder.Property(message => message.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(message => message.Url)
            .HasColumnName("url")
            .IsRequired();

        builder.Property(message => message.Caption)
            .HasColumnName("caption");

        builder.Property(message => message.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(message => message.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasOne(message => message.InboundMessageInfobip)
            .WithOne(message => message.AudioMessageInfobip)
            .HasForeignKey<AudioMessageInfobip>(message => message.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
