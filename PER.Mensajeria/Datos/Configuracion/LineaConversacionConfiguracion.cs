using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class LineaConversacionConfiguracion : IEntityTypeConfiguration<DAOLineaConversacion>
{
    public void Configure(EntityTypeBuilder<DAOLineaConversacion> builder)
    {
        builder.ToTable("per_lineas_conversacion");
        builder.HasKey(lineaConversacion => lineaConversacion.ID);
        builder.Property(lineaConversacion => lineaConversacion.ID).HasColumnName("id");
        builder.Property(lineaConversacion => lineaConversacion.IDConversacion).HasColumnName("id_conversacion");
        builder.Property(lineaConversacion => lineaConversacion.IDCompactacionContextoInicial).HasColumnName("id_compactacion_contexto_inicial");
        builder.Property(lineaConversacion => lineaConversacion.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("timestamp without time zone");
        builder.Property(lineaConversacion => lineaConversacion.FechaUltimaActividad).HasColumnName("fecha_ultima_actividad").HasColumnType("timestamp without time zone");
        builder.Property(lineaConversacion => lineaConversacion.Activa).HasColumnName("activa");

        builder.HasOne<DAOConversacion>()
            .WithMany()
            .HasForeignKey(lineaConversacion => lineaConversacion.IDConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOCompactacionContextoConversacion>()
            .WithMany()
            .HasForeignKey(lineaConversacion => lineaConversacion.IDCompactacionContextoInicial)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lineaConversacion => new { lineaConversacion.IDConversacion, lineaConversacion.Activa, lineaConversacion.FechaUltimaActividad });
        builder.HasIndex(lineaConversacion => lineaConversacion.IDCompactacionContextoInicial).IsUnique();
    }
}
