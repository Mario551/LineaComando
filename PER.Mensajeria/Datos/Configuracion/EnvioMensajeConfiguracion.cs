using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PER.Mensajeria.Datos.Configuracion;

public class EnvioMensajeConfiguracion : IEntityTypeConfiguration<DAOEnvioMensaje>
{
    public void Configure(EntityTypeBuilder<DAOEnvioMensaje> builder)
    {
        builder.ToTable("envios_mensaje");
        builder.HasKey(envioMensaje => envioMensaje.ID);
        builder.Property(envioMensaje => envioMensaje.ID).HasColumnName("id");
        builder.Property(envioMensaje => envioMensaje.IDMensaje).HasColumnName("id_mensaje");
        builder.Property(envioMensaje => envioMensaje.IDEstadoEnvioMensaje).HasColumnName("id_estado_envio_mensaje").HasMaxLength(32);
        builder.Property(envioMensaje => envioMensaje.Intentos).HasColumnName("intentos");
        builder.Property(envioMensaje => envioMensaje.Error).HasColumnName("error");
        builder.Property(envioMensaje => envioMensaje.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");
        builder.Property(envioMensaje => envioMensaje.FechaUltimoIntento).HasColumnName("fecha_ultimo_intento").HasColumnType("timestamp without time zone");
        builder.Property(envioMensaje => envioMensaje.FechaEnviado).HasColumnName("fecha_enviado").HasColumnType("timestamp without time zone");

        builder.HasOne<DAOMensaje>()
            .WithMany()
            .HasForeignKey(envioMensaje => envioMensaje.IDMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOEstadoEnvioMensaje>()
            .WithMany()
            .HasForeignKey(envioMensaje => envioMensaje.IDEstadoEnvioMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(envioMensaje => new { envioMensaje.IDEstadoEnvioMensaje, envioMensaje.FechaCreacion });
    }
}
