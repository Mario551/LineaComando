using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Datos.Configuracion;

public class EjecucionComandoContextoConfiguracion : IEntityTypeConfiguration<DAOEjecucionComandoContexto>
{
    private readonly bool esSqlServer;

    public EjecucionComandoContextoConfiguracion(bool esSqlServer)
    {
        this.esSqlServer = esSqlServer;
    }

    public void Configure(EntityTypeBuilder<DAOEjecucionComandoContexto> builder)
    {
        builder.ToTable("per_ejecuciones_comando_contexto");
        builder.HasKey(ejecucion => ejecucion.ID);
        builder.Property(ejecucion => ejecucion.ID).HasColumnName("id");
        builder.Property(ejecucion => ejecucion.IDEjecucionAnterior).HasColumnName("id_ejecucion_anterior");
        builder.Property(ejecucion => ejecucion.IDLineaConversacion).HasColumnName("id_linea_conversacion");
        builder.Property(ejecucion => ejecucion.IDProcesamientoInternoMensaje).HasColumnName("id_procesamiento_interno_mensaje");
        builder.Property(ejecucion => ejecucion.IDMetadataEntradaDecisionContextoIA).HasColumnName("id_metadata_entrada_decision_contexto_ia");
        builder.Property(ejecucion => ejecucion.IDMetadataEntradaResultadoContextoIA).HasColumnName("id_metadata_entrada_resultado_contexto_ia");
        builder.Property(ejecucion => ejecucion.NumeroIntento).HasColumnName("numero_intento");
        builder.Property(ejecucion => ejecucion.ProveedorEjecucion).HasColumnName("proveedor_ejecucion").HasMaxLength(64);
        builder.Property(ejecucion => ejecucion.IdentificadorExterno).HasColumnName("identificador_externo").HasMaxLength(128);
        builder.Property(ejecucion => ejecucion.CodigoComando).HasColumnName("codigo_comando").HasMaxLength(256);
        builder.Property(ejecucion => ejecucion.ParametrosJson).HasColumnName("parametros_json");
        builder.Property(ejecucion => ejecucion.IDEstadoEjecucionComandoContexto).HasColumnName("id_estado_ejecucion_comando_contexto").HasMaxLength(32);
        builder.Property(ejecucion => ejecucion.Activa).HasColumnName("activa");
        builder.Property(ejecucion => ejecucion.Error).HasColumnName("error");
        builder.Property(ejecucion => ejecucion.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasDefaultValueSql("LOCALTIMESTAMP");
        builder.Property(ejecucion => ejecucion.FechaInicioEncolado).HasColumnName("fecha_inicio_encolado").HasColumnType("timestamp without time zone");
        builder.Property(ejecucion => ejecucion.FechaEncolado).HasColumnName("fecha_encolado").HasColumnType("timestamp without time zone");
        builder.Property(ejecucion => ejecucion.FechaFinalizacion).HasColumnName("fecha_finalizacion").HasColumnType("timestamp without time zone");

        builder.HasOne<DAOEjecucionComandoContexto>()
            .WithMany()
            .HasForeignKey(ejecucion => ejecucion.IDEjecucionAnterior)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOLineaConversacion>()
            .WithMany()
            .HasForeignKey(ejecucion => ejecucion.IDLineaConversacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOProcesamientoInternoMensaje>()
            .WithMany()
            .HasForeignKey(ejecucion => ejecucion.IDProcesamientoInternoMensaje)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOMetadataEntradaContextoIA>()
            .WithMany()
            .HasForeignKey(ejecucion => ejecucion.IDMetadataEntradaDecisionContextoIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOMetadataEntradaContextoIA>()
            .WithMany()
            .HasForeignKey(ejecucion => ejecucion.IDMetadataEntradaResultadoContextoIA)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DAOEstadoEjecucionComandoContexto>()
            .WithMany()
            .HasForeignKey(ejecucion => ejecucion.IDEstadoEjecucionComandoContexto)
            .OnDelete(DeleteBehavior.Restrict);

        string filtroActivo = esSqlServer ? "[activa] = 1" : "\"activa\" = TRUE";
        string filtroIdentificador = esSqlServer
            ? "[identificador_externo] IS NOT NULL"
            : "\"identificador_externo\" IS NOT NULL";
        string filtroAnterior = esSqlServer
            ? "[id_ejecucion_anterior] IS NOT NULL"
            : "\"id_ejecucion_anterior\" IS NOT NULL";
        string filtroResultado = esSqlServer
            ? "[id_metadata_entrada_resultado_contexto_ia] IS NOT NULL"
            : "\"id_metadata_entrada_resultado_contexto_ia\" IS NOT NULL";

        builder.HasIndex(ejecucion => ejecucion.IDProcesamientoInternoMensaje)
            .IsUnique()
            .HasFilter(filtroActivo);
        builder.HasIndex(ejecucion => new { ejecucion.IDMetadataEntradaDecisionContextoIA, ejecucion.NumeroIntento })
            .IsUnique();
        builder.HasIndex(ejecucion => new { ejecucion.ProveedorEjecucion, ejecucion.IdentificadorExterno })
            .IsUnique()
            .HasFilter(filtroIdentificador);
        builder.HasIndex(ejecucion => ejecucion.IDEjecucionAnterior)
            .IsUnique()
            .HasFilter(filtroAnterior);
        builder.HasIndex(ejecucion => ejecucion.IDMetadataEntradaResultadoContextoIA)
            .IsUnique()
            .HasFilter(filtroResultado);
    }
}
