using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Datos.Infobip.Configuracion;

internal sealed class ProcesamientoMensajeEntranteInfobipConfiguracion :
    IEntityTypeConfiguration<DAOProcesamientoMensajeEntranteInfobip>
{
    private readonly bool incluirRelacionMensaje;

    public ProcesamientoMensajeEntranteInfobipConfiguracion()
        : this(true)
    {
    }

    internal ProcesamientoMensajeEntranteInfobipConfiguracion(bool incluirRelacionMensaje)
    {
        this.incluirRelacionMensaje = incluirRelacionMensaje;
    }

    public void Configure(EntityTypeBuilder<DAOProcesamientoMensajeEntranteInfobip> builder)
    {
        builder.ToTable("per_procesamientos_mensaje_entrante_infobip");
        builder.HasKey(procesamiento => procesamiento.ID)
            .HasName("per_pk_procesamientos_entrada_infobip");

        builder.Property(procesamiento => procesamiento.ID)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        builder.Property(procesamiento => procesamiento.IDWebhookReceiptInfobip)
            .HasColumnName("id_webhook_receipt_infobip");
        builder.Property(procesamiento => procesamiento.IDEstado)
            .HasColumnName("id_estado")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(procesamiento => procesamiento.IDMensaje)
            .HasColumnName("id_mensaje");
        builder.Property(procesamiento => procesamiento.Intentos)
            .HasColumnName("intentos")
            .HasDefaultValue(0);
        builder.Property(procesamiento => procesamiento.Error)
            .HasColumnName("error");
        builder.Property(procesamiento => procesamiento.FechaCreacion)
            .HasColumnName("fecha_creacion");
        builder.Property(procesamiento => procesamiento.FechaDespachado)
            .HasColumnName("fecha_despachado");
        builder.Property(procesamiento => procesamiento.FechaProcesado)
            .HasColumnName("fecha_procesado");

        builder.HasIndex(procesamiento => procesamiento.IDWebhookReceiptInfobip)
            .IsUnique()
            .HasDatabaseName("per_uk_proc_entrada_infobip_webhook");
        builder.HasIndex(procesamiento => procesamiento.IDMensaje)
            .IsUnique()
            .HasFilter("id_mensaje IS NOT NULL")
            .HasDatabaseName("per_uk_proc_entrada_infobip_mensaje");
        builder.HasIndex(procesamiento => new
            {
                procesamiento.IDEstado,
                procesamiento.FechaCreacion
            })
            .HasDatabaseName("per_ix_proc_entrada_infobip_estado_fecha");

        builder.HasOne(procesamiento => procesamiento.WebhookReceiptInfobip)
            .WithOne(webhook => webhook.ProcesamientoMensajeEntranteInfobip)
            .HasForeignKey<DAOProcesamientoMensajeEntranteInfobip>(
                procesamiento => procesamiento.IDWebhookReceiptInfobip)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("per_fk_proc_entrada_infobip_webhook");
        builder.HasOne<DAOEstadoProcesamientoMensajeEntranteInfobip>()
            .WithMany()
            .HasForeignKey(procesamiento => procesamiento.IDEstado)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("per_fk_proc_entrada_infobip_estado");
        if (incluirRelacionMensaje)
        {
            builder.HasOne<DAOMensaje>()
                .WithMany()
                .HasForeignKey(procesamiento => procesamiento.IDMensaje)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("per_fk_proc_entrada_infobip_mensaje");
        }
    }
}
