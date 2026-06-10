using PER.Mensajeria.Datos.Configuracion;
using PER.Mensajeria.Entidad.DAO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PER.Mensajeria.Datos.Contexto;

public class MensajeriaContextoDB : DbContext
{
    private readonly ConfiguracionMensajeriaContextoDB configuracion;

    public MensajeriaContextoDB(DbContextOptions<MensajeriaContextoDB> options)
        : this(options, null)
    {
    }

    public MensajeriaContextoDB(DbContextOptions<MensajeriaContextoDB> options, ConfiguracionMensajeriaContextoDB? configuracion) : base(options)
    {
        this.configuracion = configuracion ?? new ConfiguracionMensajeriaContextoDB();
    }

    public DbSet<DAOCanalComunicacion> CanalesComunicacion { get; set; }
    public DbSet<DAOCuentaCanal> CuentasCanal { get; set; }
    public DbSet<DAOConversacion> Conversaciones { get; set; }
    public DbSet<DAOConversacionParticipante> ConversacionesParticipantes { get; set; }
    public DbSet<DAOParticipanteConversacion> ParticipantesConversacion { get; set; }
    public DbSet<DAOTipoParticipanteConversacion> TiposParticipanteConversacion { get; set; }
    public DbSet<DAOLineaConversacion> LineasConversacion { get; set; }
    public DbSet<DAOMensaje> Mensajes { get; set; }
    public DbSet<DAODireccionMensaje> DireccionesMensaje { get; set; }
    public DbSet<DAOTipoMensaje> TiposMensaje { get; set; }
    public DbSet<DAOArchivoMensaje> ArchivosMensaje { get; set; }
    public DbSet<DAOTipoContenidoArchivo> TiposContenidoArchivo { get; set; }
    public DbSet<DAOProcesamientoInternoMensaje> ProcesamientosInternosMensaje { get; set; }
    public DbSet<DAOTipoProcesamientoInternoMensaje> TiposProcesamientoInternoMensaje { get; set; }
    public DbSet<DAOEstadoProcesamientoInternoMensaje> EstadosProcesamientoInternoMensaje { get; set; }
    public DbSet<DAOEnvioMensaje> EnviosMensaje { get; set; }
    public DbSet<DAOEstadoEnvioMensaje> EstadosEnvioMensaje { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (!string.IsNullOrWhiteSpace(configuracion.Esquema))
        {
            modelBuilder.HasDefaultSchema(configuracion.Esquema);
        }

        modelBuilder.ApplyConfiguration(new CanalComunicacionConfiguracion());
        modelBuilder.ApplyConfiguration(new CuentaCanalConfiguracion());
        modelBuilder.ApplyConfiguration(new ConversacionConfiguracion());
        modelBuilder.ApplyConfiguration(new ConversacionParticipanteConfiguracion());
        modelBuilder.ApplyConfiguration(new ParticipanteConversacionConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoParticipanteConversacionConfiguracion());
        modelBuilder.ApplyConfiguration(new LineaConversacionConfiguracion());
        modelBuilder.ApplyConfiguration(new MensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new DireccionMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new ArchivoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoContenidoArchivoConfiguracion());
        modelBuilder.ApplyConfiguration(new ProcesamientoInternoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoProcesamientoInternoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new EstadoProcesamientoInternoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new EnvioMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new EstadoEnvioMensajeConfiguracion());

        ConfigurarFechasPorProveedor(modelBuilder);
    }

    private void ConfigurarFechasPorProveedor(ModelBuilder modelBuilder)
    {
        bool esSqlServer = Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
        string tipoFecha = esSqlServer ? "datetime2" : "timestamp without time zone";
        string fechaActual = esSqlServer ? "GETDATE()" : "LOCALTIMESTAMP";

        foreach (IMutableEntityType entidad in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty propiedad in entidad.GetProperties())
            {
                Type tipoClr = Nullable.GetUnderlyingType(propiedad.ClrType) ?? propiedad.ClrType;

                if (tipoClr != typeof(DateTime))
                {
                    continue;
                }

                propiedad.SetColumnType(tipoFecha);

                if (propiedad.Name == "FechaCreacion")
                {
                    propiedad.SetDefaultValueSql(fechaActual);
                }
            }
        }
    }
}
