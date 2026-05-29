using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class EstadoEnvioMensajeConfiguracion : IEntityTypeConfiguration<DAOEstadoEnvioMensaje>
{
    public void Configure(EntityTypeBuilder<DAOEstadoEnvioMensaje> builder)
    {
        builder.ToTable("per_estados_envio_mensaje");
        builder.HasKey(estadoEnvioMensaje => estadoEnvioMensaje.ID);
        builder.Property(estadoEnvioMensaje => estadoEnvioMensaje.ID).HasColumnName("id").HasMaxLength(32);
        builder.Property(estadoEnvioMensaje => estadoEnvioMensaje.Descripcion).HasColumnName("descripcion");
    }
}
