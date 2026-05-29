using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class MensajeConfiguracion : IEntityTypeConfiguration<DAOMensaje>
{
    public void Configure(EntityTypeBuilder<DAOMensaje> builder)
    {
        builder.ToTable("per_mensajes");
        builder.HasKey(mensaje => mensaje.ID);
        builder.Property(mensaje => mensaje.ID).HasColumnName("id");
        builder.Property(mensaje => mensaje.IDLineaConversacion).HasColumnName("id_linea_conversacion");
        builder.Property(mensaje => mensaje.IDTipoMensaje).HasColumnName("id_tipo_mensaje").HasMaxLength(32);
        builder.Property(mensaje => mensaje.IDDireccionMensaje).HasColumnName("id_direccion_mensaje").HasMaxLength(32);
        builder.Property(mensaje => mensaje.TelefonoOrigen).HasColumnName("telefono_origen").HasMaxLength(64);
        builder.Property(mensaje => mensaje.TelefonoDestino).HasColumnName("telefono_destino").HasMaxLength(64);
        builder.Property(mensaje => mensaje.Contenido).HasColumnName("contenido");
        builder.Property(mensaje => mensaje.IdentificadorExternoMensaje).HasColumnName("identificador_externo_mensaje").HasMaxLength(128);
        builder.Property(mensaje => mensaje.FechaMensaje).HasColumnName("fecha_mensaje").HasColumnType("timestamp without time zone");
        builder.Property(mensaje => mensaje.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");
        builder.Property(mensaje => mensaje.FechaActualizacion).HasColumnName("fecha_actualizacion").HasColumnType("timestamp without time zone");

        builder.HasOne<DAOLineaConversacion>()
            .WithMany()
            .HasForeignKey(mensaje => mensaje.IDLineaConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOTipoMensaje>()
            .WithMany()
            .HasForeignKey(mensaje => mensaje.IDTipoMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAODireccionMensaje>()
            .WithMany()
            .HasForeignKey(mensaje => mensaje.IDDireccionMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(mensaje => new { mensaje.IDLineaConversacion, mensaje.FechaCreacion, mensaje.ID });
        builder.HasIndex(mensaje => new { mensaje.IDLineaConversacion, mensaje.IDDireccionMensaje, mensaje.IdentificadorExternoMensaje })
            .IsUnique()
            .HasFilter("identificador_externo_mensaje IS NOT NULL");
    }
}
