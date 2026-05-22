using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class TipoContenidoArchivoConfiguracion : IEntityTypeConfiguration<DAOTipoContenidoArchivo>
{
    public void Configure(EntityTypeBuilder<DAOTipoContenidoArchivo> builder)
    {
        builder.ToTable("tipos_contenido_archivo");
        builder.HasKey(tipoContenidoArchivo => tipoContenidoArchivo.ID);
        builder.Property(tipoContenidoArchivo => tipoContenidoArchivo.ID).HasColumnName("id").HasMaxLength(128);
        builder.Property(tipoContenidoArchivo => tipoContenidoArchivo.Descripcion).HasColumnName("descripcion");
    }
}
