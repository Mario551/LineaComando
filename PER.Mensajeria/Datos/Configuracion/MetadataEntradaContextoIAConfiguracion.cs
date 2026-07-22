using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class MetadataEntradaContextoIAConfiguracion : IEntityTypeConfiguration<DAOMetadataEntradaContextoIA>
{
    public void Configure(EntityTypeBuilder<DAOMetadataEntradaContextoIA> builder)
    {
        builder.ToTable("per_metadata_entradas_contexto_ia");
        builder.HasKey(entrada => entrada.ID);
        builder.Property(entrada => entrada.ID).HasColumnName("id");
        builder.Property(entrada => entrada.IDLineaConversacion).HasColumnName("id_linea_conversacion");
        builder.Property(entrada => entrada.IDMensaje).HasColumnName("id_mensaje");
        builder.Property(entrada => entrada.IDProcesamientoInternoMensaje).HasColumnName("id_procesamiento_interno_mensaje");
        builder.Property(entrada => entrada.IDInformacionTecnicaLlamadaIA).HasColumnName("id_informacion_tecnica_llamada_ia");
        builder.Property(entrada => entrada.IDCompactacionContextoIncorporada).HasColumnName("id_compactacion_contexto_incorporada");
        builder.Property(entrada => entrada.Orden).HasColumnName("orden");
        builder.Property(entrada => entrada.IDRolContextoIA).HasColumnName("id_rol_contexto_ia").HasMaxLength(32);
        builder.Property(entrada => entrada.IDTipoEntradaContextoIA).HasColumnName("id_tipo_entrada_contexto_ia").HasMaxLength(64);
        builder.Property(entrada => entrada.Contenido).HasColumnName("contenido");
        builder.Property(entrada => entrada.ToolCallID).HasColumnName("tool_call_id").HasMaxLength(128);
        builder.Property(entrada => entrada.FechaEntrada).HasColumnName("fecha_entrada").HasColumnType("timestamp without time zone");
        builder.Property(entrada => entrada.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");

        builder.HasOne<DAOLineaConversacion>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDLineaConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOMensaje>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOProcesamientoInternoMensaje>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDProcesamientoInternoMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOInformacionTecnicaLlamadaIALineaConversacion>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDInformacionTecnicaLlamadaIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOCompactacionContextoConversacion>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDCompactacionContextoIncorporada)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAORolContextoIA>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDRolContextoIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOTipoEntradaContextoIA>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDTipoEntradaContextoIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entrada => new { entrada.IDLineaConversacion, entrada.Orden });
        builder.HasIndex(entrada => new { entrada.IDProcesamientoInternoMensaje, entrada.Orden });
        builder.HasIndex(entrada => entrada.IDCompactacionContextoIncorporada);
    }
}
