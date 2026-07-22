using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;
using PER.Mensajeria.Aplicacion.EnviarMensaje;
using PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Builder.Contexto.LineaComando;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Servicio.Orquestador;

namespace BuilderTest;

public class MensajeriaBuilderTest
{
    private const string PromptAgentePrueba = "Eres un agente de prueba para mensajeria.";

    [Fact]
    public void UsarIntencionOpenRouterMiniMax_DebeRegistrarClienteTipadoAdaptadorEIntencion()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        ContextoMensajeriaBuilder builder = new(servicios);

        builder.UsarIntencionOpenRouter("api-key-prueba", openRouter => openRouter.UsarMiniMax(PromptAgentePrueba, configuracion =>
        {
            configuracion.MaximoTokens = 4321;
            configuracion.Temperatura = 0;
        }));

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        ConfiguracionMiniMaxOpenRouter configuracion = serviceProvider.GetRequiredService<ConfiguracionMiniMaxOpenRouter>();

        Assert.Equal(4321, configuracion.MaximoTokens);
        Assert.Equal(0, configuracion.Temperatura);
        Assert.Equal(PromptAgentePrueba, configuracion.PromptAgente);
        Assert.IsType<MiniMaxOpenRouterAdaptador>(serviceProvider.GetRequiredService<IOpenRouterModeloAdaptador>());
        Assert.IsType<OpenRouterCliente>(serviceProvider.GetRequiredService<IOpenRouterCliente>());
        Assert.IsType<OpenRouterIntencionContextoServicio>(serviceProvider.GetRequiredService<IIntencionContextoConversacionServicio>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UsarIntencionOpenRouterMiniMax_PromptInvalido_DebeFallar(string? promptAgente)
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        Assert.ThrowsAny<ArgumentException>(() =>
            builder.UsarIntencionOpenRouter(
                "api-key-prueba",
                openRouter => openRouter.UsarMiniMax(promptAgente!)));
    }

    [Fact]
    public void UsarIntencionOpenRouter_SinModelo_DebeFallar()
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        InvalidOperationException excepcion = Assert.Throws<InvalidOperationException>(() =>
            builder.UsarIntencionOpenRouter("api-key-prueba", _ => { }));

        Assert.Contains("adapter de modelo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsarIntencionPersonalizada_DespuesDeOpenRouter_DebeReemplazarContratoDeIntencion()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        ContextoMensajeriaBuilder builder = new(servicios);
        builder.UsarIntencionOpenRouter(
            "api-key-prueba",
            openRouter => openRouter.UsarMiniMax(PromptAgentePrueba));

        builder.UsarIntencion<IntencionPersonalizadaPrueba>();

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        Assert.IsType<IntencionPersonalizadaPrueba>(serviceProvider.GetRequiredService<IIntencionContextoConversacionServicio>());
    }

    [Fact]
    public void AgregarWorkerOrquestador_DebeRegistrarHostedService()
    {
        ServiceCollection servicios = new();

        servicios.AgregarMensajeria(builder => builder.AgregarWorkerOrquestador());

        Assert.Contains(servicios, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Contains(servicios, descriptor => descriptor.ServiceType == typeof(ICompactacionContextoConversacionAplicacion));
        Assert.Contains(servicios, descriptor => descriptor.ServiceType == typeof(IRenovarLineaContextoAplicacion));
        Assert.Contains(servicios, descriptor => descriptor.ServiceType == typeof(IEjecucionComandoContextoAplicacion));
    }

    [Fact]
    public void AgregarMensajeria_DebeRegistrarCiclosDeVidaDelOrquestador()
    {
        ServiceCollection servicios = new();

        servicios.AgregarMensajeria(_ => { });

        Assert.Equal(
            ServiceLifetime.Singleton,
            ObtenerDescriptor(servicios, typeof(IOrquestadorContextoServicio)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            ObtenerDescriptor(servicios, typeof(IUnitOfWorkFactory)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            ObtenerDescriptor(servicios, typeof(IUnitOfWork)).Lifetime);

        Type[] aplicacionesScoped =
        [
            typeof(ICargarEventosMensajeriaPendientesAplicacion),
            typeof(IRegistrarMensajeEntranteAplicacion),
            typeof(IRegistrarMensajeSalidaAplicacion),
            typeof(IEnviarMensajeAplicacion),
            typeof(IOrquestarMensajeEntradaAplicacion),
            typeof(IRegistrarContextoIAAplicacion),
            typeof(IEjecucionComandoContextoAplicacion),
            typeof(ICompactacionContextoConversacionAplicacion),
            typeof(IRenovarLineaContextoAplicacion),
            typeof(IContextoConversacionServicio)
        ];

        Assert.All(
            aplicacionesScoped,
            tipoServicio => Assert.Equal(
                ServiceLifetime.Scoped,
                ObtenerDescriptor(servicios, tipoServicio).Lifetime));
    }

    [Fact]
    public async Task AgregarMensajeria_DebeResolverSingletonsSinCapturarServiciosScoped()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        servicios.AgregarMensajeria(_ => { });

        await using ServiceProvider serviceProvider = servicios.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

        IOrquestadorContextoServicio primerOrquestador = serviceProvider
            .GetRequiredService<IOrquestadorContextoServicio>();
        IOrquestadorContextoServicio segundoOrquestador = serviceProvider
            .GetRequiredService<IOrquestadorContextoServicio>();
        IUnitOfWorkFactory primeraFactory = serviceProvider.GetRequiredService<IUnitOfWorkFactory>();
        IUnitOfWorkFactory segundaFactory = serviceProvider.GetRequiredService<IUnitOfWorkFactory>();

        Assert.Same(primerOrquestador, segundoOrquestador);
        Assert.Same(primeraFactory, segundaFactory);
    }

    [Fact]
    public void AgregarMensajeria_DebeRegistrarConfiguracionOrquestadorPredeterminada()
    {
        ServiceCollection servicios = new();

        servicios.AgregarMensajeria(_ => { });

        ServiceDescriptor descriptor = ObtenerDescriptor(
            servicios,
            typeof(ConfiguracionOrquestadorContexto));
        ConfiguracionOrquestadorContexto configuracion = Assert.IsType<ConfiguracionOrquestadorContexto>(
            descriptor.ImplementationInstance);

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(16, configuracion.MaximoConversacionesConcurrentes);
    }

    [Fact]
    public void ConfigurarOrquestadorContexto_DebeReemplazarConfiguracionPredeterminada()
    {
        ServiceCollection servicios = new();
        ConfiguracionOrquestadorContexto configuracionEsperada = new()
        {
            MaximoConversacionesConcurrentes = 5
        };

        servicios.AgregarMensajeria(builder =>
            builder.ConfigurarOrquestadorContexto(configuracionEsperada));

        ServiceDescriptor descriptor = ObtenerDescriptor(
            servicios,
            typeof(ConfiguracionOrquestadorContexto));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(configuracionEsperada, descriptor.ImplementationInstance);
    }

    [Fact]
    public void UsarEjecutorLineaComando_DebeRegistrarAdapterProductivo()
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        builder.UsarEjecutorLineaComando();

        ServiceDescriptor descriptor = Assert.Single(
            servicios,
            descriptorActual => descriptorActual.ServiceType == typeof(IEjecutorComandoContextoServicio));
        Assert.Equal(typeof(EjecutorComandoLineaComandoServicio), descriptor.ImplementationType);
    }

    [Fact]
    public void AgregarMensajeria_PostgreSql_DebeUsarConexionEsquemaYRegistrarInicializador()
    {
        ServiceCollection servicios = new();
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) => Task.CompletedTask);
        lineaComandoBuilder.UsePostgresql(
            "Host=localhost;Database=lineacomando;Username=postgres;Password=123456789",
            "mensajeria_test");

        lineaComandoBuilder.AgregarMensajeria(_ => { });

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        using MensajeriaContextoDB contexto = serviceProvider.GetRequiredService<MensajeriaContextoDB>();
        NpgsqlConnectionStringBuilder builderConexion = new(contexto.Database.GetDbConnection().ConnectionString);

        Assert.Equal("mensajeria_test", builderConexion.SearchPath);
        Assert.Equal("mensajeria_test", contexto.Model.GetDefaultSchema());
        Assert.Single(lineaComandoBuilder.InicializadoresExternos);
    }

    [Fact]
    public void AgregarMensajeria_SqlServer_DebeUsarConexionEsquemaYRegistrarInicializador()
    {
        ServiceCollection servicios = new();
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) => Task.CompletedTask);
        lineaComandoBuilder.UseSqlServer(
            "Server=localhost;Database=lineacomando;User Id=sa;Password=ClaveTemporal123;TrustServerCertificate=True",
            "mensajeria_sql");

        lineaComandoBuilder.AgregarMensajeria(_ => { });

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        using MensajeriaContextoDB contexto = serviceProvider.GetRequiredService<MensajeriaContextoDB>();

        Assert.Contains("SqlServer", contexto.Database.ProviderName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mensajeria_sql", contexto.Model.GetDefaultSchema());
        Assert.Single(lineaComandoBuilder.InicializadoresExternos);
    }

    [Fact]
    public void AgregarMensajeria_Sqlite_DebeRechazarMotorNoSoportado()
    {
        ServiceCollection servicios = new();
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) => Task.CompletedTask);
        lineaComandoBuilder.UseSqlite("Data Source=:memory:");

        Assert.Throws<NotSupportedException>(() => lineaComandoBuilder.AgregarMensajeria(_ => { }));
    }

    [Fact]
    public async Task InicializarLineaComandoAsync_DebeEjecutarInicializadoresExternosAntesDeConfiguracionComandos()
    {
        ServiceCollection servicios = new();
        List<string> ejecuciones = [];
        LineaComandoBuilder lineaComandoBuilder = new(servicios, (_, _, _) =>
        {
            ejecuciones.Add("configuracion");
            return Task.CompletedTask;
        });

        lineaComandoBuilder.AgregarInicializadorExterno((_, _, _) =>
        {
            ejecuciones.Add("externo");
            return Task.CompletedTask;
        });

        servicios.AddSingleton(lineaComandoBuilder);
        servicios.AddSingleton(new FactoriaComandos<string, ResultadoComando>());
        servicios.AddSingleton<IRegistroComandos<string, ResultadoComando>>(new RegistroComandosFake(ejecuciones));

        using ServiceProvider serviceProvider = servicios.BuildServiceProvider();

        await serviceProvider.InicializarLineaComandoAsync();

        Assert.Equal(["externo", "configuracion", "factoria"], ejecuciones);
    }

    private sealed class RegistroComandosFake : IRegistroComandos<string, ResultadoComando>
    {
        private readonly IList<string> ejecuciones;

        public RegistroComandosFake(IList<string> ejecuciones)
        {
            this.ejecuciones = ejecuciones;
        }

        public IDictionary<string, MetadatosComando> ComandosRegistrados { get; } = new Dictionary<string, MetadatosComando>();

        public Task<IEnumerable<MetadatosComando>> ObtenerComandosRegistradosAsync(CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<MetadatosComando>>([]);
        }

        public Task ConstruirFactoriaAsync(FactoriaComandos<string, ResultadoComando> factoria, CancellationToken token = default)
        {
            ejecuciones.Add("factoria");
            return Task.CompletedTask;
        }

        public Task RegistrarComandoAsync(MetadatosComando metadatos, IComandoCreador<string, ResultadoComando> comandoCreador, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task EliminarRegistroComandoAsync(string rutaComando, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
    }

    private static ServiceDescriptor ObtenerDescriptor(
        IServiceCollection servicios,
        Type tipoServicio)
    {
        return Assert.Single(
            servicios,
            descriptor => descriptor.ServiceType == tipoServicio);
    }

    private sealed class IntencionPersonalizadaPrueba : IIntencionContextoConversacionServicio
    {
        public Task<ResultadoIntencionContexto> DecidirAsync(
            SolicitudIntencionContexto solicitud,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ResultadoCompactacionIntencionContexto> CompactarAsync(
            SolicitudCompactacionIntencionContexto solicitud,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
