using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class CompactacionContextoConversacionConfiguracion : IEntityTypeConfiguration<DAOCompactacionContextoConversacion>
{
    public void Configure(EntityTypeBuilder<DAOCompactacionContextoConversacion> builder)
    {
        builder.ToTable("per_compactaciones_contexto_conversacion");
        builder.HasKey(compactacion => compactacion.ID);
        builder.Property(compactacion => compactacion.ID).HasColumnName("id");
        builder.Property(compactacion => compactacion.IDConversacion).HasColumnName("id_conversacion");
        builder.Property(compactacion => compactacion.IDLineaConversacionOrigen).HasColumnName("id_linea_conversacion_origen");
        builder.Property(compactacion => compactacion.IDCompactacionContextoAnterior).HasColumnName("id_compactacion_contexto_anterior");
        builder.Property(compactacion => compactacion.IDInformacionTecnicaLlamadaIA).HasColumnName("id_informacion_tecnica_llamada_ia");
        builder.Property(compactacion => compactacion.Version).HasColumnName("version");
        builder.Property(compactacion => compactacion.Contenido).HasColumnName("contenido");
        builder.Property(compactacion => compactacion.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("LOCALTIMESTAMP");

        builder.HasOne<DAOConversacion>()
            .WithMany()
            .HasForeignKey(compactacion => compactacion.IDConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOLineaConversacion>()
            .WithMany()
            .HasForeignKey(compactacion => compactacion.IDLineaConversacionOrigen)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOCompactacionContextoConversacion>()
            .WithMany()
            .HasForeignKey(compactacion => compactacion.IDCompactacionContextoAnterior)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOInformacionTecnicaLlamadaIALineaConversacion>()
            .WithMany()
            .HasForeignKey(compactacion => compactacion.IDInformacionTecnicaLlamadaIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(compactacion => compactacion.IDLineaConversacionOrigen).IsUnique();
        builder.HasIndex(compactacion => new { compactacion.IDConversacion, compactacion.Version }).IsUnique();
        builder.HasIndex(compactacion => compactacion.IDCompactacionContextoAnterior);
        builder.HasIndex(compactacion => compactacion.IDInformacionTecnicaLlamadaIA).IsUnique();
    }
}
