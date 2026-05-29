using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class ParticipanteConversacionConfiguracion : IEntityTypeConfiguration<DAOParticipanteConversacion>
{
    public void Configure(EntityTypeBuilder<DAOParticipanteConversacion> builder)
    {
        builder.ToTable("per_participantes_conversacion");
        builder.HasKey(participanteConversacion => participanteConversacion.ID);
        builder.Property(participanteConversacion => participanteConversacion.ID).HasColumnName("id");
        builder.Property(participanteConversacion => participanteConversacion.IDTipoParticipanteConversacion).HasColumnName("id_tipo_participante_conversacion").HasMaxLength(32);
        builder.Property(participanteConversacion => participanteConversacion.IdentificadorParticipante).HasColumnName("identificador_participante").HasMaxLength(256);

        builder.HasOne<DAOTipoParticipanteConversacion>()
            .WithMany()
            .HasForeignKey(participanteConversacion => participanteConversacion.IDTipoParticipanteConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(participanteConversacion => new { participanteConversacion.IDTipoParticipanteConversacion, participanteConversacion.IdentificadorParticipante }).IsUnique();
    }
}
