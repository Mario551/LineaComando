using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class TipoProcesamientoInternoMensajeConfiguracion : IEntityTypeConfiguration<DAOTipoProcesamientoInternoMensaje>
{
    public void Configure(EntityTypeBuilder<DAOTipoProcesamientoInternoMensaje> builder)
    {
        builder.ToTable("per_tipos_procesamiento_interno_mensaje");
        builder.HasKey(tipoProcesamientoInternoMensaje => tipoProcesamientoInternoMensaje.ID);
        builder.Property(tipoProcesamientoInternoMensaje => tipoProcesamientoInternoMensaje.ID).HasColumnName("id").HasMaxLength(128);
        builder.Property(tipoProcesamientoInternoMensaje => tipoProcesamientoInternoMensaje.Descripcion).HasColumnName("descripcion");
    }
}
