using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class EstadoProcesamientoMensajeEntranteInfobipConfiguracion :
    IEntityTypeConfiguration<DAOEstadoProcesamientoMensajeEntranteInfobip>
{
    public void Configure(EntityTypeBuilder<DAOEstadoProcesamientoMensajeEntranteInfobip> builder)
    {
        builder.ToTable("per_estados_procesamiento_mensaje_entrante_infobip");
        builder.HasKey(estado => estado.ID)
            .HasName("per_pk_estados_proc_entrada_infobip");

        builder.Property(estado => estado.ID)
            .HasColumnName("id")
            .HasMaxLength(32);
        builder.Property(estado => estado.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasData(
            new DAOEstadoProcesamientoMensajeEntranteInfobip
            {
                ID = "pendiente",
                Descripcion = "Pendiente de entregar al flujo generico de mensajeria"
            },
            new DAOEstadoProcesamientoMensajeEntranteInfobip
            {
                ID = "despachado",
                Descripcion = "Entregado al worker y pendiente de confirmacion"
            },
            new DAOEstadoProcesamientoMensajeEntranteInfobip
            {
                ID = "procesado",
                Descripcion = "Relacionado con el mensaje generico persistido"
            },
            new DAOEstadoProcesamientoMensajeEntranteInfobip
            {
                ID = "error",
                Descripcion = "No se pudo convertir o despachar al flujo generico"
            });
    }
}
