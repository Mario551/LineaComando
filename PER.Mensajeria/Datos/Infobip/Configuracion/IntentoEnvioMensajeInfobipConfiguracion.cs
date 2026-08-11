using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class IntentoEnvioMensajeInfobipConfiguracion :
    IEntityTypeConfiguration<DAOIntentoEnvioMensajeInfobip>
{
    private readonly bool incluirRelacionEnvioMensaje;

    public IntentoEnvioMensajeInfobipConfiguracion()
        : this(true)
    {
    }

    internal IntentoEnvioMensajeInfobipConfiguracion(bool incluirRelacionEnvioMensaje)
    {
        this.incluirRelacionEnvioMensaje = incluirRelacionEnvioMensaje;
    }

    public void Configure(EntityTypeBuilder<DAOIntentoEnvioMensajeInfobip> builder)
    {
        builder.ToTable("per_intentos_envio_mensaje_infobip");
        builder.HasKey(intento => intento.ID)
            .HasName("per_pk_intentos_envio_infobip");

        builder.Property(intento => intento.ID)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        builder.Property(intento => intento.IDEnvioMensaje)
            .HasColumnName("id_envio_mensaje");
        builder.Property(intento => intento.NumeroIntento)
            .HasColumnName("numero_intento");
        builder.Property(intento => intento.IDEstado)
            .HasColumnName("id_estado")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(intento => intento.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(256);
        builder.Property(intento => intento.SolicitudJson)
            .HasColumnName("solicitud_json");
        builder.Property(intento => intento.RespuestaJson)
            .HasColumnName("respuesta_json");
        builder.Property(intento => intento.StatusHttp)
            .HasColumnName("status_http");
        builder.Property(intento => intento.MessageIDInfobip)
            .HasColumnName("message_id_infobip")
            .HasMaxLength(256);
        builder.Property(intento => intento.IDGrupoEstadoInfobip)
            .HasColumnName("id_grupo_estado_infobip");
        builder.Property(intento => intento.GrupoEstadoInfobip)
            .HasColumnName("grupo_estado_infobip")
            .HasMaxLength(64);
        builder.Property(intento => intento.IDEstadoInfobip)
            .HasColumnName("id_estado_infobip");
        builder.Property(intento => intento.EstadoInfobip)
            .HasColumnName("estado_infobip")
            .HasMaxLength(128);
        builder.Property(intento => intento.DescripcionEstadoInfobip)
            .HasColumnName("descripcion_estado_infobip");
        builder.Property(intento => intento.Error)
            .HasColumnName("error");
        builder.Property(intento => intento.FechaInicio)
            .HasColumnName("fecha_inicio");
        builder.Property(intento => intento.FechaFinalizacion)
            .HasColumnName("fecha_finalizacion");

        builder.HasOne<DAOEstadoIntentoEnvioMensajeInfobip>()
            .WithMany()
            .HasForeignKey(intento => intento.IDEstado)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("per_fk_intento_envio_infobip_estado");

        if (incluirRelacionEnvioMensaje)
        {
            builder.HasOne<DAOEnvioMensaje>()
                .WithMany()
                .HasForeignKey(intento => intento.IDEnvioMensaje)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("per_fk_intento_envio_infobip_envio");
        }

        builder.HasIndex(intento => new
            {
                intento.IDEnvioMensaje,
                intento.NumeroIntento
            })
            .IsUnique()
            .HasDatabaseName("per_uk_intento_envio_infobip_numero");
        builder.HasIndex(intento => intento.MessageIDInfobip)
            .HasDatabaseName("per_ix_intento_envio_infobip_message");
        builder.HasIndex(intento => new
            {
                intento.IDEstado,
                intento.FechaInicio
            })
            .HasDatabaseName("per_ix_intento_envio_infobip_estado_fecha");
    }
}
