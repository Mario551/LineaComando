using PER.Mensajeria.Datos.Configuracion;
using PER.Mensajeria.Datos.Infobip.Configuracion;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;
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
    public DbSet<DAOCompactacionContextoConversacion> CompactacionesContextoConversacion { get; set; }
    public DbSet<DAOMensaje> Mensajes { get; set; }
    public DbSet<DAODireccionMensaje> DireccionesMensaje { get; set; }
    public DbSet<DAOTipoMensaje> TiposMensaje { get; set; }
    public DbSet<DAOArchivoMensaje> ArchivosMensaje { get; set; }
    public DbSet<DAOTipoContenidoArchivo> TiposContenidoArchivo { get; set; }
    public DbSet<DAOProcesamientoInternoMensaje> ProcesamientosInternosMensaje { get; set; }
    public DbSet<DAOTipoProcesamientoInternoMensaje> TiposProcesamientoInternoMensaje { get; set; }
    public DbSet<DAOEstadoProcesamientoInternoMensaje> EstadosProcesamientoInternoMensaje { get; set; }
    public DbSet<DAORolContextoIA> RolesContextoIA { get; set; }
    public DbSet<DAOTipoEntradaContextoIA> TiposEntradaContextoIA { get; set; }
    public DbSet<DAOMetadataEntradaContextoIA> MetadataEntradasContextoIA { get; set; }
    public DbSet<DAOInformacionTecnicaLlamadaIALineaConversacion> InformacionTecnicaLlamadasIALineaConversacion { get; set; }
    public DbSet<DAOEstadoEjecucionComandoContexto> EstadosEjecucionComandoContexto { get; set; }
    public DbSet<DAOEjecucionComandoContexto> EjecucionesComandoContexto { get; set; }
    public DbSet<DAOEnvioMensaje> EnviosMensaje { get; set; }
    public DbSet<DAOEstadoEnvioMensaje> EstadosEnvioMensaje { get; set; }
    public DbSet<WebhookReceiptInfobip> WebhookReceiptsInfobip { get; set; }
    public DbSet<MessageTypeInfobip> MessageTypesInfobip { get; set; }
    public DbSet<InboundMessageInfobip> InboundMessagesInfobip { get; set; }
    public DbSet<MessageContextInfobip> MessageContextsInfobip { get; set; }
    public DbSet<MessageReferralInfobip> MessageReferralsInfobip { get; set; }
    public DbSet<TextMessageInfobip> TextMessagesInfobip { get; set; }
    public DbSet<LocationMessageInfobip> LocationMessagesInfobip { get; set; }
    public DbSet<ImageMessageInfobip> ImageMessagesInfobip { get; set; }
    public DbSet<DocumentMessageInfobip> DocumentMessagesInfobip { get; set; }
    public DbSet<AudioMessageInfobip> AudioMessagesInfobip { get; set; }
    public DbSet<VideoMessageInfobip> VideoMessagesInfobip { get; set; }
    public DbSet<VoiceMessageInfobip> VoiceMessagesInfobip { get; set; }
    public DbSet<ContactMessageInfobip> ContactMessagesInfobip { get; set; }
    public DbSet<InfectedContentMessageInfobip> InfectedContentMessagesInfobip { get; set; }
    public DbSet<ButtonMessageInfobip> ButtonMessagesInfobip { get; set; }
    public DbSet<StickerMessageInfobip> StickerMessagesInfobip { get; set; }
    public DbSet<InteractiveButtonReplyMessageInfobip> InteractiveButtonReplyMessagesInfobip { get; set; }
    public DbSet<InteractiveListReplyMessageInfobip> InteractiveListReplyMessagesInfobip { get; set; }
    public DbSet<FlowReplyMessageInfobip> FlowReplyMessagesInfobip { get; set; }
    public DbSet<PaymentConfirmationMessageInfobip> PaymentConfirmationMessagesInfobip { get; set; }
    public DbSet<CallPermissionReplyMessageInfobip> CallPermissionReplyMessagesInfobip { get; set; }
    public DbSet<InThreadAuthenticationReplyMessageInfobip> InThreadAuthenticationReplyMessagesInfobip { get; set; }
    public DbSet<OrderMessageInfobip> OrderMessagesInfobip { get; set; }
    public DbSet<ReactionMessageInfobip> ReactionMessagesInfobip { get; set; }
    public DbSet<UnsupportedMessageInfobip> UnsupportedMessagesInfobip { get; set; }
    public DbSet<SharedContactInfobip> SharedContactsInfobip { get; set; }
    public DbSet<ContactAddressInfobip> ContactAddressesInfobip { get; set; }
    public DbSet<ContactEmailInfobip> ContactEmailsInfobip { get; set; }
    public DbSet<ContactPhoneInfobip> ContactPhonesInfobip { get; set; }
    public DbSet<ContactUrlInfobip> ContactUrlsInfobip { get; set; }
    public DbSet<OrderProductItemInfobip> OrderProductItemsInfobip { get; set; }
    public DbSet<FlowResponseNodeInfobip> FlowResponseNodesInfobip { get; set; }
    public DbSet<DAOEstadoProcesamientoMensajeEntranteInfobip> EstadosProcesamientoMensajeEntranteInfobip { get; set; }
    public DbSet<DAOProcesamientoMensajeEntranteInfobip> ProcesamientosMensajeEntranteInfobip { get; set; }
    public DbSet<DAOEstadoIntentoEnvioMensajeInfobip> EstadosIntentoEnvioMensajeInfobip { get; set; }
    public DbSet<DAOIntentoEnvioMensajeInfobip> IntentosEnvioMensajeInfobip { get; set; }

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
        modelBuilder.ApplyConfiguration(new CompactacionContextoConversacionConfiguracion());
        modelBuilder.ApplyConfiguration(new MensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new DireccionMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new ArchivoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoContenidoArchivoConfiguracion());
        modelBuilder.ApplyConfiguration(new ProcesamientoInternoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoProcesamientoInternoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new EstadoProcesamientoInternoMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new RolContextoIAConfiguracion());
        modelBuilder.ApplyConfiguration(new TipoEntradaContextoIAConfiguracion());
        modelBuilder.ApplyConfiguration(new MetadataEntradaContextoIAConfiguracion());
        modelBuilder.ApplyConfiguration(new InformacionTecnicaLlamadaIALineaConversacionConfiguracion());
        modelBuilder.ApplyConfiguration(new EstadoEjecucionComandoContextoConfiguracion());
        bool esSqlServer = Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
        modelBuilder.ApplyConfiguration(new EjecucionComandoContextoConfiguracion(esSqlServer));
        modelBuilder.ApplyConfiguration(new EnvioMensajeConfiguracion());
        modelBuilder.ApplyConfiguration(new EstadoEnvioMensajeConfiguracion());
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WebhookReceiptInfobipConfiguracion).Assembly,
            tipo => tipo.Namespace == typeof(WebhookReceiptInfobipConfiguracion).Namespace);

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
