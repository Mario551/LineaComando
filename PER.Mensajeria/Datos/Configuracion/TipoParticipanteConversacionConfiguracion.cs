using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class TipoParticipanteConversacionConfiguracion : IEntityTypeConfiguration<DAOTipoParticipanteConversacion>
{
    public void Configure(EntityTypeBuilder<DAOTipoParticipanteConversacion> builder)
    {
        builder.ToTable("per_tipos_participante_conversacion");
        builder.HasKey(tipoParticipanteConversacion => tipoParticipanteConversacion.ID);
        builder.Property(tipoParticipanteConversacion => tipoParticipanteConversacion.ID).HasColumnName("id").HasMaxLength(32);
        builder.Property(tipoParticipanteConversacion => tipoParticipanteConversacion.Descripcion).HasColumnName("descripcion");
    }
}
