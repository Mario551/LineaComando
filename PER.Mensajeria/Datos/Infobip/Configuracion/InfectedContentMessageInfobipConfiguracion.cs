using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class InfectedContentMessageInfobipConfiguracion : IEntityTypeConfiguration<InfectedContentMessageInfobip>
{
    public void Configure(EntityTypeBuilder<InfectedContentMessageInfobip> builder)
    {
        builder.ToTable("per_infected_content_messages_infobip");

        builder.HasKey(message => message.RecordId);

        builder.Property(message => message.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(message => message.Malware)
            .HasColumnName("malware");

        builder.Property(message => message.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(message => message.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasOne(message => message.InboundMessageInfobip)
            .WithOne(message => message.InfectedContentMessageInfobip)
            .HasForeignKey<InfectedContentMessageInfobip>(message => message.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
