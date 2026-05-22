using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class ArchivoMensajeConfiguracion : IEntityTypeConfiguration<DAOArchivoMensaje>
{
    public void Configure(EntityTypeBuilder<DAOArchivoMensaje> builder)
    {
        builder.ToTable("archivos_mensaje");
        builder.HasKey(archivoMensaje => archivoMensaje.ID);
        builder.Property(archivoMensaje => archivoMensaje.ID).HasColumnName("id");
        builder.Property(archivoMensaje => archivoMensaje.IDMensaje).HasColumnName("id_mensaje");
        builder.Property(archivoMensaje => archivoMensaje.IDTipoContenidoArchivo).HasColumnName("id_tipo_contenido_archivo").HasMaxLength(128);
        builder.Property(archivoMensaje => archivoMensaje.NombreArchivo).HasColumnName("nombre_archivo");
        builder.Property(archivoMensaje => archivoMensaje.TamanoBytes).HasColumnName("tamano_bytes");
        builder.Property(archivoMensaje => archivoMensaje.UbicacionArchivo).HasColumnName("ubicacion_archivo");
        builder.Property(archivoMensaje => archivoMensaje.ProveedorAlmacenamiento).HasColumnName("proveedor_almacenamiento").HasMaxLength(64);
        builder.Property(archivoMensaje => archivoMensaje.IdentificadorExternoArchivo).HasColumnName("identificador_externo_archivo").HasMaxLength(256);
        builder.Property(archivoMensaje => archivoMensaje.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");

        builder.HasOne<DAOMensaje>()
            .WithMany()
            .HasForeignKey(archivoMensaje => archivoMensaje.IDMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOTipoContenidoArchivo>()
            .WithMany()
            .HasForeignKey(archivoMensaje => archivoMensaje.IDTipoContenidoArchivo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
