using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class EntradaContextoIAConfiguracion : IEntityTypeConfiguration<DAOEntradaContextoIA>
{
    public void Configure(EntityTypeBuilder<DAOEntradaContextoIA> builder)
    {
        builder.ToTable("per_entradas_contexto_ia");
        builder.HasKey(entrada => entrada.ID);
        builder.Property(entrada => entrada.ID).HasColumnName("id");
        builder.Property(entrada => entrada.IDLineaConversacion).HasColumnName("id_linea_conversacion");
        builder.Property(entrada => entrada.IDMensaje).HasColumnName("id_mensaje");
        builder.Property(entrada => entrada.IDProcesamientoInternoMensaje).HasColumnName("id_procesamiento_interno_mensaje");
        builder.Property(entrada => entrada.IDMetadataRazonamientoIA).HasColumnName("id_metadata_razonamiento_ia");
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

        builder.HasOne<DAOMetadataRazonamientoIALineaConversacion>()
            .WithMany()
            .HasForeignKey(entrada => entrada.IDMetadataRazonamientoIA)
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
    }
}
