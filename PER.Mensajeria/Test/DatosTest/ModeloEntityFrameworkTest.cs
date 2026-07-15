using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;

namespace DatosTest;

public class ModeloEntityFrameworkTest
{
    [Fact]
    public void Modelo_DebeUsarTablasConPrefijoPer()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        Assert.Equal("per_mensajes", ObtenerEntidad(contexto, typeof(DAOMensaje)).GetTableName());
        Assert.Equal("per_lineas_conversacion", ObtenerEntidad(contexto, typeof(DAOLineaConversacion)).GetTableName());
        Assert.Equal("per_procesamientos_internos_mensaje", ObtenerEntidad(contexto, typeof(DAOProcesamientoInternoMensaje)).GetTableName());
        Assert.Equal("per_envios_mensaje", ObtenerEntidad(contexto, typeof(DAOEnvioMensaje)).GetTableName());
        Assert.Equal("per_roles_contexto_ia", ObtenerEntidad(contexto, typeof(DAORolContextoIA)).GetTableName());
        Assert.Equal("per_tipos_entrada_contexto_ia", ObtenerEntidad(contexto, typeof(DAOTipoEntradaContextoIA)).GetTableName());
        Assert.Equal("per_entradas_contexto_ia", ObtenerEntidad(contexto, typeof(DAOEntradaContextoIA)).GetTableName());
        Assert.Equal("per_estados_contexto_conversacion", ObtenerEntidad(contexto, typeof(DAOEstadoContextoConversacion)).GetTableName());
        Assert.Equal(
            "per_metadata_razonamiento_ia_linea_conversacion",
            ObtenerEntidad(contexto, typeof(DAOMetadataRazonamientoIALineaConversacion)).GetTableName());
    }

    [Fact]
    public void Mensaje_DebeTenerIndiceParcialDeIdempotencia()
    {
        using MensajeriaContextoDB contexto = CrearContexto();
        IEntityType entidadMensaje = ObtenerEntidad(contexto, typeof(DAOMensaje));

        IIndex? indice = entidadMensaje.GetIndexes().SingleOrDefault(indiceActual =>
            indiceActual.Properties.Select(propiedad => propiedad.Name).SequenceEqual(
            [
                nameof(DAOMensaje.IDLineaConversacion),
                nameof(DAOMensaje.IDDireccionMensaje),
                nameof(DAOMensaje.IdentificadorExternoMensaje)
            ]));

        Assert.NotNull(indice);
        Assert.True(indice.IsUnique);
        Assert.Equal("identificador_externo_mensaje IS NOT NULL", indice.GetFilter());
    }

    [Fact]
    public void Fechas_DebenConfigurarseComoTimestampSinZonaHoraria()
    {
        using MensajeriaContextoDB contexto = CrearContexto();
        IEntityType entidadMensaje = ObtenerEntidad(contexto, typeof(DAOMensaje));
        IEntityType entidadEnvio = ObtenerEntidad(contexto, typeof(DAOEnvioMensaje));

        Assert.Equal("timestamp without time zone", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaMensaje))?.GetColumnType());
        Assert.Equal("timestamp without time zone", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaCreacion))?.GetColumnType());
        Assert.Equal("timestamp without time zone", entidadEnvio.FindProperty(nameof(DAOEnvioMensaje.FechaEnviado))?.GetColumnType());
    }

    [Fact]
    public void ModeloSqlServer_DebeUsarEsquemaTiposFechaYDefaultSqlServer()
    {
        using MensajeriaContextoDB contexto = CrearContextoSqlServer("mensajeria_sql_test");
        IEntityType entidadMensaje = ObtenerEntidad(contexto, typeof(DAOMensaje));
        IEntityType entidadEnvio = ObtenerEntidad(contexto, typeof(DAOEnvioMensaje));
        IEntityType entidadEntradaContextoIA = ObtenerEntidad(contexto, typeof(DAOEntradaContextoIA));
        IEntityType entidadMetadataIA = ObtenerEntidad(contexto, typeof(DAOMetadataRazonamientoIALineaConversacion));
        IEntityType entidadEstadoContexto = ObtenerEntidad(contexto, typeof(DAOEstadoContextoConversacion));

        Assert.Contains("SqlServer", contexto.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mensajeria_sql_test", contexto.Model.GetDefaultSchema());
        Assert.Equal("datetime2", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaMensaje))?.GetColumnType());
        Assert.Equal("datetime2", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaCreacion))?.GetColumnType());
        Assert.Equal("GETDATE()", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaCreacion))?.GetDefaultValueSql());
        Assert.Equal("datetime2", entidadEnvio.FindProperty(nameof(DAOEnvioMensaje.FechaEnviado))?.GetColumnType());
        Assert.Equal("datetime2", entidadEntradaContextoIA.FindProperty(nameof(DAOEntradaContextoIA.FechaEntrada))?.GetColumnType());
        Assert.Equal("datetime2", entidadMetadataIA.FindProperty(nameof(DAOMetadataRazonamientoIALineaConversacion.FechaCreacion))?.GetColumnType());
        Assert.Equal("datetime2", entidadEstadoContexto.FindProperty(nameof(DAOEstadoContextoConversacion.FechaCreacion))?.GetColumnType());
        AssertModeloContextoIA(contexto);
        AssertModeloSnapshot(contexto);
    }

    [Fact]
    public void ContextoIA_DebeConfigurarCuatroIndicesYNueveRelaciones()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        AssertModeloContextoIA(contexto);
    }

    [Fact]
    public void Snapshot_DebeConfigurarRelacionesYUnicidades()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        AssertModeloSnapshot(contexto);
    }

    private static MensajeriaContextoDB CrearContexto()
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseNpgsql("Host=localhost;Database=per_mensajeria_modelo;Username=test;Password=test")
            .Options;

        return new MensajeriaContextoDB(opciones);
    }

    private static MensajeriaContextoDB CrearContextoSqlServer(string esquema)
    {
        DbContextOptions<MensajeriaContextoDB> opciones = new DbContextOptionsBuilder<MensajeriaContextoDB>()
            .UseSqlServer("Server=localhost;Database=per_mensajeria_modelo;User Id=sa;Password=Pass123!;TrustServerCertificate=True")
            .Options;

        return new MensajeriaContextoDB(opciones, new ConfiguracionMensajeriaContextoDB { Esquema = esquema });
    }

    private static IEntityType ObtenerEntidad(MensajeriaContextoDB contexto, Type tipo)
    {
        IEntityType? entidad = contexto.Model.FindEntityType(tipo);
        Assert.NotNull(entidad);
        return entidad;
    }

    private static void AssertModeloContextoIA(MensajeriaContextoDB contexto)
    {
        IEntityType entrada = ObtenerEntidad(contexto, typeof(DAOEntradaContextoIA));
        IEntityType metadata = ObtenerEntidad(contexto, typeof(DAOMetadataRazonamientoIALineaConversacion));

        AssertIndice(
            entrada,
            nameof(DAOEntradaContextoIA.IDLineaConversacion),
            nameof(DAOEntradaContextoIA.Orden));
        AssertIndice(
            entrada,
            nameof(DAOEntradaContextoIA.IDProcesamientoInternoMensaje),
            nameof(DAOEntradaContextoIA.Orden));
        AssertIndice(
            metadata,
            nameof(DAOMetadataRazonamientoIALineaConversacion.IDLineaConversacion),
            nameof(DAOMetadataRazonamientoIALineaConversacion.Iteracion));
        AssertIndice(
            metadata,
            nameof(DAOMetadataRazonamientoIALineaConversacion.IDProcesamientoInternoMensaje),
            nameof(DAOMetadataRazonamientoIALineaConversacion.Iteracion));

        Assert.Equal(6, entrada.GetForeignKeys().Count());
        Assert.Equal(3, metadata.GetForeignKeys().Count());
        AssertRelaciones(
            entrada,
            typeof(DAOLineaConversacion),
            typeof(DAOMensaje),
            typeof(DAOProcesamientoInternoMensaje),
            typeof(DAOMetadataRazonamientoIALineaConversacion),
            typeof(DAORolContextoIA),
            typeof(DAOTipoEntradaContextoIA));
        AssertRelaciones(
            metadata,
            typeof(DAOLineaConversacion),
            typeof(DAOMensaje),
            typeof(DAOProcesamientoInternoMensaje));
    }

    private static void AssertModeloSnapshot(MensajeriaContextoDB contexto)
    {
        IEntityType estado = ObtenerEntidad(contexto, typeof(DAOEstadoContextoConversacion));
        IEntityType linea = ObtenerEntidad(contexto, typeof(DAOLineaConversacion));

        Assert.Equal(4, estado.GetForeignKeys().Count());
        AssertRelaciones(
            estado,
            typeof(DAOConversacion),
            typeof(DAOLineaConversacion),
            typeof(DAOEstadoContextoConversacion),
            typeof(DAOMetadataRazonamientoIALineaConversacion));
        AssertIndiceUnico(estado, nameof(DAOEstadoContextoConversacion.IDLineaConversacionOrigen));
        AssertIndiceUnico(
            estado,
            nameof(DAOEstadoContextoConversacion.IDConversacion),
            nameof(DAOEstadoContextoConversacion.Version));
        AssertIndice(estado, nameof(DAOEstadoContextoConversacion.IDEstadoContextoAnterior));
        AssertIndiceUnico(estado, nameof(DAOEstadoContextoConversacion.IDMetadataRazonamientoIA));
        AssertIndiceUnico(linea, nameof(DAOLineaConversacion.IDEstadoContextoInicial));
        Assert.Contains(
            linea.GetForeignKeys(),
            llave => llave.PrincipalEntityType.ClrType == typeof(DAOEstadoContextoConversacion));
    }

    private static void AssertIndice(IEntityType entidad, params string[] propiedades)
    {
        Assert.Contains(
            entidad.GetIndexes(),
            indice => indice.Properties.Select(propiedad => propiedad.Name).SequenceEqual(propiedades));
    }

    private static void AssertIndiceUnico(IEntityType entidad, params string[] propiedades)
    {
        IIndex? indice = entidad.GetIndexes().SingleOrDefault(
            indiceActual => indiceActual.Properties
                .Select(propiedad => propiedad.Name)
                .SequenceEqual(propiedades));

        Assert.NotNull(indice);
        Assert.True(indice.IsUnique);
    }

    private static void AssertRelaciones(IEntityType entidad, params Type[] principales)
    {
        Type[] relaciones = entidad.GetForeignKeys()
            .Select(llave => llave.PrincipalEntityType.ClrType)
            .OrderBy(tipo => tipo.FullName)
            .ToArray();
        Type[] esperadas = principales.OrderBy(tipo => tipo.FullName).ToArray();
        Assert.Equal(esperadas, relaciones);
    }
}
