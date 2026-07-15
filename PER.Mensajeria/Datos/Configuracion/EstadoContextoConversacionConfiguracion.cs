using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class EstadoContextoConversacionConfiguracion : IEntityTypeConfiguration<DAOEstadoContextoConversacion>
{
    public void Configure(EntityTypeBuilder<DAOEstadoContextoConversacion> builder)
    {
        builder.ToTable("per_estados_contexto_conversacion");
        builder.HasKey(estado => estado.ID);
        builder.Property(estado => estado.ID).HasColumnName("id");
        builder.Property(estado => estado.IDConversacion).HasColumnName("id_conversacion");
        builder.Property(estado => estado.IDLineaConversacionOrigen).HasColumnName("id_linea_conversacion_origen");
        builder.Property(estado => estado.IDEstadoContextoAnterior).HasColumnName("id_estado_contexto_anterior");
        builder.Property(estado => estado.IDMetadataRazonamientoIA).HasColumnName("id_metadata_razonamiento_ia");
        builder.Property(estado => estado.Version).HasColumnName("version");
        builder.Property(estado => estado.Contenido).HasColumnName("contenido");
        builder.Property(estado => estado.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("LOCALTIMESTAMP");

        builder.HasOne<DAOConversacion>()
            .WithMany()
            .HasForeignKey(estado => estado.IDConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOLineaConversacion>()
            .WithMany()
            .HasForeignKey(estado => estado.IDLineaConversacionOrigen)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOEstadoContextoConversacion>()
            .WithMany()
            .HasForeignKey(estado => estado.IDEstadoContextoAnterior)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOMetadataRazonamientoIALineaConversacion>()
            .WithMany()
            .HasForeignKey(estado => estado.IDMetadataRazonamientoIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(estado => estado.IDLineaConversacionOrigen).IsUnique();
        builder.HasIndex(estado => new { estado.IDConversacion, estado.Version }).IsUnique();
        builder.HasIndex(estado => estado.IDEstadoContextoAnterior);
        builder.HasIndex(estado => estado.IDMetadataRazonamientoIA).IsUnique();
    }
}
