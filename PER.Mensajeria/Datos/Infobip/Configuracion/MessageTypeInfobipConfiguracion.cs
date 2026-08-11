using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class MessageTypeInfobipConfiguracion : IEntityTypeConfiguration<MessageTypeInfobip>
{
    public void Configure(EntityTypeBuilder<MessageTypeInfobip> builder)
    {
        builder.ToTable(
            "per_message_types_infobip",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "per_ck_message_types_infobip_type_not_empty",
                "TRIM(type) <> ''"));

        builder.HasKey(entity => entity.Type)
            .HasName("per_pk_message_types_infobip");

        builder.Property(entity => entity.Type)
            .HasColumnName("type")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(entity => entity.RecordCreatedAt)
            .HasColumnName("record_created_at")
            .IsRequired();

        builder.Property(entity => entity.RecordUpdatedAt)
            .HasColumnName("record_updated_at");

        DateTime seedDate = new(2026, 7, 30, 0, 0, 0, DateTimeKind.Unspecified);

        builder.HasData(
            new MessageTypeInfobip { Type = "TEXT", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "LOCATION", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "IMAGE", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "DOCUMENT", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "AUDIO", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "VIDEO", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "VOICE", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "CONTACT", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INFECTED_CONTENT", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "BUTTON", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "STICKER", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INTERACTIVE_BUTTON_REPLY", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INTERACTIVE_LIST_REPLY", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INTERACTIVE_FLOW_REPLY", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INTERACTIVE_PAYMENT_CONFIRMATION", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INTERACTIVE_CALL_PERMISSION_REPLY", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "INTERACTIVE_IN_THREAD_AUTHENTICATION_REPLY", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "ORDER", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "REACTION", RecordCreatedAt = seedDate },
            new MessageTypeInfobip { Type = "UNSUPPORTED", RecordCreatedAt = seedDate });
    }
}
