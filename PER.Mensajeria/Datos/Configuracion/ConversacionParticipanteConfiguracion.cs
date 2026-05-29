using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class ConversacionParticipanteConfiguracion : IEntityTypeConfiguration<DAOConversacionParticipante>
{
    public void Configure(EntityTypeBuilder<DAOConversacionParticipante> builder)
    {
        builder.ToTable("per_conversaciones_participantes");
        builder.HasKey(conversacionParticipante => conversacionParticipante.ID);
        builder.Property(conversacionParticipante => conversacionParticipante.ID).HasColumnName("id");
        builder.Property(conversacionParticipante => conversacionParticipante.IDConversacion).HasColumnName("id_conversacion");
        builder.Property(conversacionParticipante => conversacionParticipante.IDParticipanteConversacion).HasColumnName("id_participante_conversacion");
        builder.Property(conversacionParticipante => conversacionParticipante.FechaUnion).HasColumnName("fecha_union").HasColumnType("timestamp without time zone");
        builder.Property(conversacionParticipante => conversacionParticipante.FechaSalida).HasColumnName("fecha_salida").HasColumnType("timestamp without time zone");
        builder.Property(conversacionParticipante => conversacionParticipante.Activo).HasColumnName("activo");

        builder.HasOne<DAOConversacion>()
            .WithMany()
            .HasForeignKey(conversacionParticipante => conversacionParticipante.IDConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOParticipanteConversacion>()
            .WithMany()
            .HasForeignKey(conversacionParticipante => conversacionParticipante.IDParticipanteConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(conversacionParticipante => new { conversacionParticipante.IDConversacion, conversacionParticipante.Activo });
    }
}
