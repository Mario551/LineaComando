using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class ConversacionConfiguracion : IEntityTypeConfiguration<DAOConversacion>
{
    public void Configure(EntityTypeBuilder<DAOConversacion> builder)
    {
        builder.ToTable("per_conversaciones");
        builder.HasKey(conversacion => conversacion.ID);
        builder.Property(conversacion => conversacion.ID).HasColumnName("id");
        builder.Property(conversacion => conversacion.IDCuentaCanal).HasColumnName("id_cuenta_canal");
        builder.Property(conversacion => conversacion.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");
        builder.Property(conversacion => conversacion.FechaActualizacion).HasColumnName("fecha_actualizacion").HasColumnType("timestamp without time zone");

        builder.HasOne<DAOCuentaCanal>()
            .WithMany()
            .HasForeignKey(conversacion => conversacion.IDCuentaCanal)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
