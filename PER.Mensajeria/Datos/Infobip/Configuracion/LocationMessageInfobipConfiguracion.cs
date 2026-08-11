using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class LocationMessageInfobipConfiguracion : IEntityTypeConfiguration<LocationMessageInfobip>
{
    public void Configure(EntityTypeBuilder<LocationMessageInfobip> builder)
    {
        builder.ToTable(
            "per_location_messages_infobip",
            table =>
            {
                table.HasCheckConstraint(
                    "per_ck_location_messages_infobip_latitude",
                    "latitude >= -90 AND latitude <= 90");
                table.HasCheckConstraint(
                    "per_ck_location_messages_infobip_longitude",
                    "longitude >= -180 AND longitude <= 180");
            });

        builder.HasKey(message => message.RecordId);

        builder.Property(message => message.RecordId)
            .HasColumnName("record_id")
            .ValueGeneratedOnAdd();

        builder.Property(message => message.RecordIdInboundMessagesInfobip)
            .HasColumnName("record_id_inbound_messages_infobip")
            .IsRequired();

        builder.Property(message => message.Latitude)
            .HasColumnName("latitude")
            .IsRequired();

        builder.Property(message => message.Longitude)
            .HasColumnName("longitude")
            .IsRequired();

        builder.Property(message => message.Address)
            .HasColumnName("address");

        builder.Property(message => message.Name)
            .HasColumnName("name");

        builder.Property(message => message.Url)
            .HasColumnName("url");

        builder.Property(message => message.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(message => message.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        builder.HasOne(message => message.InboundMessageInfobip)
            .WithOne(message => message.LocationMessageInfobip)
            .HasForeignKey<LocationMessageInfobip>(message => message.RecordIdInboundMessagesInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
