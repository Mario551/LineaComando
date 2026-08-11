using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class EstadoIntentoEnvioMensajeInfobipConfiguracion :
    IEntityTypeConfiguration<DAOEstadoIntentoEnvioMensajeInfobip>
{
    public void Configure(EntityTypeBuilder<DAOEstadoIntentoEnvioMensajeInfobip> builder)
    {
        builder.ToTable("per_estados_intento_envio_mensaje_infobip");
        builder.HasKey(estado => estado.ID)
            .HasName("per_pk_estados_intento_envio_infobip");

        builder.Property(estado => estado.ID)
            .HasColumnName("id")
            .HasMaxLength(32);
        builder.Property(estado => estado.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasData(
            new DAOEstadoIntentoEnvioMensajeInfobip
            {
                ID = "enviando",
                Descripcion = "Solicitud preparada y potencialmente enviada a Infobip"
            },
            new DAOEstadoIntentoEnvioMensajeInfobip
            {
                ID = "aceptado",
                Descripcion = "Solicitud aceptada por Infobip para procesamiento"
            },
            new DAOEstadoIntentoEnvioMensajeInfobip
            {
                ID = "fallido",
                Descripcion = "Solicitud rechazada o invalida antes de ser aceptada"
            },
            new DAOEstadoIntentoEnvioMensajeInfobip
            {
                ID = "incierto",
                Descripcion = "No fue posible determinar si Infobip acepto la solicitud"
            });
    }
}
