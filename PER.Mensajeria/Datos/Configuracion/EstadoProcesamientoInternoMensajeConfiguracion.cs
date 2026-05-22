using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class EstadoProcesamientoInternoMensajeConfiguracion : IEntityTypeConfiguration<DAOEstadoProcesamientoInternoMensaje>
{
    public void Configure(EntityTypeBuilder<DAOEstadoProcesamientoInternoMensaje> builder)
    {
        builder.ToTable("estados_procesamiento_interno_mensaje");
        builder.HasKey(estadoProcesamientoInternoMensaje => estadoProcesamientoInternoMensaje.ID);
        builder.Property(estadoProcesamientoInternoMensaje => estadoProcesamientoInternoMensaje.ID).HasColumnName("id").HasMaxLength(128);
        builder.Property(estadoProcesamientoInternoMensaje => estadoProcesamientoInternoMensaje.Descripcion).HasColumnName("descripcion");
    }
}
