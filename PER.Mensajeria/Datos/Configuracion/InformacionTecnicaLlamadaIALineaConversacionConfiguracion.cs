using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class InformacionTecnicaLlamadaIALineaConversacionConfiguracion : IEntityTypeConfiguration<DAOInformacionTecnicaLlamadaIALineaConversacion>
{
    public void Configure(EntityTypeBuilder<DAOInformacionTecnicaLlamadaIALineaConversacion> builder)
    {
        builder.ToTable("per_informacion_tecnica_llamadas_ia_linea_conversacion");
        builder.HasKey(metadata => metadata.ID);
        builder.Property(metadata => metadata.ID).HasColumnName("id");
        builder.Property(metadata => metadata.IDLineaConversacion).HasColumnName("id_linea_conversacion");
        builder.Property(metadata => metadata.IDProcesamientoInternoMensaje).HasColumnName("id_procesamiento_interno_mensaje");
        builder.Property(metadata => metadata.IDMensaje).HasColumnName("id_mensaje");
        builder.Property(metadata => metadata.Proveedor).HasColumnName("proveedor").HasMaxLength(128);
        builder.Property(metadata => metadata.Modelo).HasColumnName("modelo").HasMaxLength(256);
        builder.Property(metadata => metadata.Adaptador).HasColumnName("adaptador").HasMaxLength(256);
        builder.Property(metadata => metadata.Iteracion).HasColumnName("iteracion");
        builder.Property(metadata => metadata.AccionDecidida).HasColumnName("accion_decidida").HasMaxLength(64);
        builder.Property(metadata => metadata.FinishReason).HasColumnName("finish_reason").HasMaxLength(128);
        builder.Property(metadata => metadata.NativeFinishReason).HasColumnName("native_finish_reason").HasMaxLength(128);
        builder.Property(metadata => metadata.PromptTokens).HasColumnName("prompt_tokens");
        builder.Property(metadata => metadata.CompletionTokens).HasColumnName("completion_tokens");
        builder.Property(metadata => metadata.ReasoningTokens).HasColumnName("reasoning_tokens");
        builder.Property(metadata => metadata.TotalTokens).HasColumnName("total_tokens");
        builder.Property(metadata => metadata.RequestJson).HasColumnName("request_json");
        builder.Property(metadata => metadata.ResponseJson).HasColumnName("response_json");
        builder.Property(metadata => metadata.Content).HasColumnName("content");
        builder.Property(metadata => metadata.Reasoning).HasColumnName("reasoning");
        builder.Property(metadata => metadata.ReasoningDetailsJson).HasColumnName("reasoning_details_json");
        builder.Property(metadata => metadata.Error).HasColumnName("error");
        builder.Property(metadata => metadata.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");

        builder.HasOne<DAOLineaConversacion>()
            .WithMany()
            .HasForeignKey(metadata => metadata.IDLineaConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOProcesamientoInternoMensaje>()
            .WithMany()
            .HasForeignKey(metadata => metadata.IDProcesamientoInternoMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOMensaje>()
            .WithMany()
            .HasForeignKey(metadata => metadata.IDMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(metadata => new { metadata.IDLineaConversacion, metadata.Iteracion });
        builder.HasIndex(metadata => new { metadata.IDProcesamientoInternoMensaje, metadata.Iteracion });
    }
}
