using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PER.Mensajeria.Datos.Infobip.Configuracion;

namespace PER.Mensajeria.Datos.Infobip.Esquema;

internal sealed class ContextoInfobipInicializacionDB : DbContext
{
    private readonly string esquema;

    public ContextoInfobipInicializacionDB(
        DbContextOptions<ContextoInfobipInicializacionDB> options,
        string esquema)
        : base(options)
    {
        this.esquema = esquema;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WebhookReceiptInfobipConfiguracion).Assembly,
            tipo => tipo.Namespace == typeof(WebhookReceiptInfobipConfiguracion).Namespace
                && tipo != typeof(ProcesamientoMensajeEntranteInfobipConfiguracion)
                && tipo != typeof(IntentoEnvioMensajeInfobipConfiguracion));
        modelBuilder.ApplyConfiguration(
            new ProcesamientoMensajeEntranteInfobipConfiguracion(false));
        modelBuilder.ApplyConfiguration(
            new IntentoEnvioMensajeInfobipConfiguracion(false));
        ConfigurarFechasPorProveedor(modelBuilder);
    }

    private void ConfigurarFechasPorProveedor(ModelBuilder modelBuilder)
    {
        bool esSqlServer = Database.ProviderName?.Contains(
            "SqlServer",
            StringComparison.OrdinalIgnoreCase) == true;
        string tipoFecha = esSqlServer ? "datetime2" : "timestamp without time zone";

        foreach (IMutableEntityType entidad in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty propiedad in entidad.GetProperties())
            {
                Type tipoClr = Nullable.GetUnderlyingType(propiedad.ClrType)
                    ?? propiedad.ClrType;

                if (tipoClr == typeof(DateTime))
                {
                    propiedad.SetColumnType(tipoFecha);
                }
            }
        }
    }
}
