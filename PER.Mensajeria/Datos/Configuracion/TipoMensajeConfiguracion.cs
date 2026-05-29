using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class TipoMensajeConfiguracion : IEntityTypeConfiguration<DAOTipoMensaje>
{
    public void Configure(EntityTypeBuilder<DAOTipoMensaje> builder)
    {
        builder.ToTable("per_tipos_mensaje");
        builder.HasKey(tipoMensaje => tipoMensaje.ID);
        builder.Property(tipoMensaje => tipoMensaje.ID).HasColumnName("id").HasMaxLength(32);
        builder.Property(tipoMensaje => tipoMensaje.Descripcion).HasColumnName("descripcion");
    }
}
