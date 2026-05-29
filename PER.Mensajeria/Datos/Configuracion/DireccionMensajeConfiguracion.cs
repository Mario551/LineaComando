using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class DireccionMensajeConfiguracion : IEntityTypeConfiguration<DAODireccionMensaje>
{
    public void Configure(EntityTypeBuilder<DAODireccionMensaje> builder)
    {
        builder.ToTable("per_direcciones_mensaje");
        builder.HasKey(direccionMensaje => direccionMensaje.ID);
        builder.Property(direccionMensaje => direccionMensaje.ID).HasColumnName("id").HasMaxLength(32);
        builder.Property(direccionMensaje => direccionMensaje.Descripcion).HasColumnName("descripcion");
    }
}
