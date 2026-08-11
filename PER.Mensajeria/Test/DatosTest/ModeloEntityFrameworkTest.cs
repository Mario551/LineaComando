using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;

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
        Assert.Equal("per_metadata_entradas_contexto_ia", ObtenerEntidad(contexto, typeof(DAOMetadataEntradaContextoIA)).GetTableName());
        Assert.Equal("per_compactaciones_contexto_conversacion", ObtenerEntidad(contexto, typeof(DAOCompactacionContextoConversacion)).GetTableName());
        Assert.Equal("per_estados_ejecucion_comando_contexto", ObtenerEntidad(contexto, typeof(DAOEstadoEjecucionComandoContexto)).GetTableName());
        Assert.Equal("per_ejecuciones_comando_contexto", ObtenerEntidad(contexto, typeof(DAOEjecucionComandoContexto)).GetTableName());
        Assert.Equal(
            "per_informacion_tecnica_llamadas_ia_linea_conversacion",
            ObtenerEntidad(contexto, typeof(DAOInformacionTecnicaLlamadaIALineaConversacion)).GetTableName());
        Assert.Equal(
            "per_estados_intento_envio_mensaje_infobip",
            ObtenerEntidad(contexto, typeof(DAOEstadoIntentoEnvioMensajeInfobip)).GetTableName());
        Assert.Equal(
            "per_intentos_envio_mensaje_infobip",
            ObtenerEntidad(contexto, typeof(DAOIntentoEnvioMensajeInfobip)).GetTableName());
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
        IEntityType entidadMetadataEntradaContextoIA = ObtenerEntidad(contexto, typeof(DAOMetadataEntradaContextoIA));
        IEntityType entidadInformacionTecnicaLlamadasIA = ObtenerEntidad(contexto, typeof(DAOInformacionTecnicaLlamadaIALineaConversacion));
        IEntityType entidadCompactacionContexto = ObtenerEntidad(contexto, typeof(DAOCompactacionContextoConversacion));
        IEntityType entidadEjecucionComando = ObtenerEntidad(contexto, typeof(DAOEjecucionComandoContexto));

        Assert.Contains("SqlServer", contexto.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mensajeria_sql_test", contexto.Model.GetDefaultSchema());
        Assert.Equal("datetime2", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaMensaje))?.GetColumnType());
        Assert.Equal("datetime2", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaCreacion))?.GetColumnType());
        Assert.Equal("GETDATE()", entidadMensaje.FindProperty(nameof(DAOMensaje.FechaCreacion))?.GetDefaultValueSql());
        Assert.Equal("datetime2", entidadEnvio.FindProperty(nameof(DAOEnvioMensaje.FechaEnviado))?.GetColumnType());
        Assert.Equal("datetime2", entidadMetadataEntradaContextoIA.FindProperty(nameof(DAOMetadataEntradaContextoIA.FechaEntrada))?.GetColumnType());
        Assert.Equal("datetime2", entidadInformacionTecnicaLlamadasIA.FindProperty(nameof(DAOInformacionTecnicaLlamadaIALineaConversacion.FechaCreacion))?.GetColumnType());
        Assert.Equal("datetime2", entidadCompactacionContexto.FindProperty(nameof(DAOCompactacionContextoConversacion.FechaCreacion))?.GetColumnType());
        Assert.Equal("datetime2", entidadEjecucionComando.FindProperty(nameof(DAOEjecucionComandoContexto.FechaEncolado))?.GetColumnType());
        AssertModeloContextoIA(contexto);
        AssertModeloCompactacionContexto(contexto);
        AssertModeloEjecucionComando(contexto, true);
    }

    [Fact]
    public void ContextoIA_DebeConfigurarCincoIndicesYDiezRelaciones()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        AssertModeloContextoIA(contexto);
    }

    [Fact]
    public void Snapshot_DebeConfigurarRelacionesYUnicidades()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        AssertModeloCompactacionContexto(contexto);
    }

    [Fact]
    public void EjecucionComando_DebeConfigurarRelacionesYUnicidadesPostgreSql()
    {
        using MensajeriaContextoDB contexto = CrearContexto();

        AssertModeloEjecucionComando(contexto, false);
    }

    [Fact]
    public void IntentoEnvioInfobip_DebeConfigurarRelacionesEIndices()
    {
        using MensajeriaContextoDB contexto = CrearContexto();
        IEntityType intento = ObtenerEntidad(
            contexto,
            typeof(DAOIntentoEnvioMensajeInfobip));

        AssertRelaciones(
            intento,
            typeof(DAOEnvioMensaje),
            typeof(DAOEstadoIntentoEnvioMensajeInfobip));
        AssertIndiceUnico(
            intento,
            nameof(DAOIntentoEnvioMensajeInfobip.IDEnvioMensaje),
            nameof(DAOIntentoEnvioMensajeInfobip.NumeroIntento));
        AssertIndice(
            intento,
            nameof(DAOIntentoEnvioMensajeInfobip.MessageIDInfobip));
        AssertIndice(
            intento,
            nameof(DAOIntentoEnvioMensajeInfobip.IDEstado),
            nameof(DAOIntentoEnvioMensajeInfobip.FechaInicio));
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
        IEntityType entrada = ObtenerEntidad(contexto, typeof(DAOMetadataEntradaContextoIA));
        IEntityType metadata = ObtenerEntidad(contexto, typeof(DAOInformacionTecnicaLlamadaIALineaConversacion));

        AssertIndice(
            entrada,
            nameof(DAOMetadataEntradaContextoIA.IDLineaConversacion),
            nameof(DAOMetadataEntradaContextoIA.Orden));
        AssertIndice(
            entrada,
            nameof(DAOMetadataEntradaContextoIA.IDProcesamientoInternoMensaje),
            nameof(DAOMetadataEntradaContextoIA.Orden));
        AssertIndice(entrada, nameof(DAOMetadataEntradaContextoIA.IDCompactacionContextoIncorporada));
        AssertIndice(
            metadata,
            nameof(DAOInformacionTecnicaLlamadaIALineaConversacion.IDLineaConversacion),
            nameof(DAOInformacionTecnicaLlamadaIALineaConversacion.Iteracion));
        AssertIndice(
            metadata,
            nameof(DAOInformacionTecnicaLlamadaIALineaConversacion.IDProcesamientoInternoMensaje),
            nameof(DAOInformacionTecnicaLlamadaIALineaConversacion.Iteracion));

        Assert.Equal(7, entrada.GetForeignKeys().Count());
        Assert.Equal(3, metadata.GetForeignKeys().Count());
        AssertRelaciones(
            entrada,
            typeof(DAOLineaConversacion),
            typeof(DAOMensaje),
            typeof(DAOProcesamientoInternoMensaje),
            typeof(DAOInformacionTecnicaLlamadaIALineaConversacion),
            typeof(DAOCompactacionContextoConversacion),
            typeof(DAORolContextoIA),
            typeof(DAOTipoEntradaContextoIA));
        AssertRelaciones(
            metadata,
            typeof(DAOLineaConversacion),
            typeof(DAOMensaje),
            typeof(DAOProcesamientoInternoMensaje));
    }

    private static void AssertModeloCompactacionContexto(MensajeriaContextoDB contexto)
    {
        IEntityType compactacion = ObtenerEntidad(contexto, typeof(DAOCompactacionContextoConversacion));
        IEntityType linea = ObtenerEntidad(contexto, typeof(DAOLineaConversacion));

        Assert.Equal(4, compactacion.GetForeignKeys().Count());
        AssertRelaciones(
            compactacion,
            typeof(DAOConversacion),
            typeof(DAOLineaConversacion),
            typeof(DAOCompactacionContextoConversacion),
            typeof(DAOInformacionTecnicaLlamadaIALineaConversacion));
        AssertIndiceUnico(compactacion, nameof(DAOCompactacionContextoConversacion.IDLineaConversacionOrigen));
        AssertIndiceUnico(
            compactacion,
            nameof(DAOCompactacionContextoConversacion.IDConversacion),
            nameof(DAOCompactacionContextoConversacion.Version));
        AssertIndice(compactacion, nameof(DAOCompactacionContextoConversacion.IDCompactacionContextoAnterior));
        AssertIndiceUnico(compactacion, nameof(DAOCompactacionContextoConversacion.IDInformacionTecnicaLlamadaIA));
        AssertIndiceUnico(linea, nameof(DAOLineaConversacion.IDCompactacionContextoInicial));
        Assert.Contains(
            linea.GetForeignKeys(),
            llave => llave.PrincipalEntityType.ClrType == typeof(DAOCompactacionContextoConversacion));
    }

    private static void AssertModeloEjecucionComando(
        MensajeriaContextoDB contexto,
        bool esSqlServer)
    {
        IEntityType ejecucion = ObtenerEntidad(contexto, typeof(DAOEjecucionComandoContexto));

        Assert.Equal(6, ejecucion.GetForeignKeys().Count());
        AssertRelaciones(
            ejecucion,
            typeof(DAOEjecucionComandoContexto),
            typeof(DAOLineaConversacion),
            typeof(DAOProcesamientoInternoMensaje),
            typeof(DAOMetadataEntradaContextoIA),
            typeof(DAOMetadataEntradaContextoIA),
            typeof(DAOEstadoEjecucionComandoContexto));
        AssertIndiceUnico(ejecucion, nameof(DAOEjecucionComandoContexto.IDProcesamientoInternoMensaje));
        AssertIndiceUnico(
            ejecucion,
            nameof(DAOEjecucionComandoContexto.IDMetadataEntradaDecisionContextoIA),
            nameof(DAOEjecucionComandoContexto.NumeroIntento));
        AssertIndiceUnico(
            ejecucion,
            nameof(DAOEjecucionComandoContexto.ProveedorEjecucion),
            nameof(DAOEjecucionComandoContexto.IdentificadorExterno));
        AssertIndiceUnico(ejecucion, nameof(DAOEjecucionComandoContexto.IDEjecucionAnterior));
        AssertIndiceUnico(ejecucion, nameof(DAOEjecucionComandoContexto.IDMetadataEntradaResultadoContextoIA));

        IIndex indiceActivo = ejecucion.GetIndexes().Single(indice =>
            indice.Properties.Select(propiedad => propiedad.Name)
                .SequenceEqual([nameof(DAOEjecucionComandoContexto.IDProcesamientoInternoMensaje)]));
        Assert.Equal(esSqlServer ? "[activa] = 1" : "\"activa\" = TRUE", indiceActivo.GetFilter());
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
