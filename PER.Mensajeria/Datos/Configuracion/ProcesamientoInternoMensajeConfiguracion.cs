using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class ProcesamientoInternoMensajeConfiguracion : IEntityTypeConfiguration<DAOProcesamientoInternoMensaje>
{
    public void Configure(EntityTypeBuilder<DAOProcesamientoInternoMensaje> builder)
    {
        builder.ToTable("per_procesamientos_internos_mensaje");
        builder.HasKey(procesamientoInternoMensaje => procesamientoInternoMensaje.ID);
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.ID).HasColumnName("id");
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.IDMensaje).HasColumnName("id_mensaje");
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.IDTipoProcesamientoInternoMensaje).HasColumnName("id_tipo_procesamiento_interno_mensaje").HasMaxLength(128);
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.IDEstadoProcesamientoInternoMensaje).HasColumnName("id_estado_procesamiento_interno_mensaje").HasMaxLength(128);
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.Intentos).HasColumnName("intentos");
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.Error).HasColumnName("error");
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");
        builder.Property(procesamientoInternoMensaje => procesamientoInternoMensaje.FechaProcesado).HasColumnName("fecha_procesado").HasColumnType("timestamp without time zone");

        builder.HasOne<DAOMensaje>()
            .WithMany()
            .HasForeignKey(procesamientoInternoMensaje => procesamientoInternoMensaje.IDMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOTipoProcesamientoInternoMensaje>()
            .WithMany()
            .HasForeignKey(procesamientoInternoMensaje => procesamientoInternoMensaje.IDTipoProcesamientoInternoMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOEstadoProcesamientoInternoMensaje>()
            .WithMany()
            .HasForeignKey(procesamientoInternoMensaje => procesamientoInternoMensaje.IDEstadoProcesamientoInternoMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(procesamientoInternoMensaje => new { procesamientoInternoMensaje.IDEstadoProcesamientoInternoMensaje, procesamientoInternoMensaje.FechaCreacion });
    }
}
