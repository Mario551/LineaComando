using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Mensajeria.API.Comunicacion;
using PER.Mensajeria.API.Infobip;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;
using PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;
using PER.Mensajeria.Aplicacion.Infobip.Envio;
using PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;
using PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;
using PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;
using PER.Mensajeria.Aplicacion.RenovarLineaContexto;
using PER.Mensajeria.Builder.Contexto.LineaComando;
using PER.Mensajeria.Builder.Worker;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Infobip;
using PER.Mensajeria.Servicio.Mensaje;
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

    [Fact]
    public void UsarIntencionOpenCode_DebeRegistrarClienteTipadoAdaptadorEIntencion()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        ContextoMensajeriaBuilder builder = new(servicios);

        builder.UsarIntencionOpenCode(
            PromptAgentePrueba,
            "mensajeria-contexto",
            configuracion =>
            {
                configuracion.Servidor =
                    new Uri("http://opencode:4096");
                configuracion.AutenticacionBasica =
                    new ConfiguracionAutenticacionBasicaOpenCode(
                        "opencode",
                        "clave-secreta");
                configuracion.Timeout = TimeSpan.FromMinutes(4);
            });

        using ServiceProvider serviceProvider =
            servicios.BuildServiceProvider();
        ConfiguracionIntencionOpenCode configuracion = serviceProvider
            .GetRequiredService<ConfiguracionIntencionOpenCode>();
        OpenCodeCliente cliente = Assert.IsType<OpenCodeCliente>(
            serviceProvider.GetRequiredService<IOpenCodeCliente>());
        HttpClient httpClient = ObtenerHttpClient(cliente);

        Assert.Equal(PromptAgentePrueba, configuracion.PromptAgente);
        Assert.Equal("mensajeria-contexto", configuracion.NombreAgente);
        Assert.Equal(TimeSpan.FromMinutes(4), configuracion.Timeout);
        Assert.Equal(
            new Uri("http://opencode:4096/"),
            httpClient.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(4), httpClient.Timeout);
        Assert.Equal(
            "Basic",
            httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        string credenciales = Encoding.UTF8.GetString(
            Convert.FromBase64String(
                httpClient.DefaultRequestHeaders.Authorization!.Parameter!));
        Assert.Equal("opencode:clave-secreta", credenciales);
        Assert.IsType<OpenCodeAgenteAdaptador>(
            serviceProvider.GetRequiredService<IOpenCodeAgenteAdaptador>());
        Assert.IsType<OpenCodeIntencionContextoServicio>(
            serviceProvider.GetRequiredService<
                IIntencionContextoConversacionServicio>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UsarIntencionOpenCode_PromptInvalido_DebeFallar(
        string? promptAgente)
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        Assert.ThrowsAny<ArgumentException>(() =>
            builder.UsarIntencionOpenCode(
                promptAgente!,
                "mensajeria-contexto",
                _ => { }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UsarIntencionOpenCode_AgenteInvalido_DebeFallar(
        string? agente)
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        Assert.ThrowsAny<ArgumentException>(() =>
            builder.UsarIntencionOpenCode(
                PromptAgentePrueba,
                agente!,
                _ => { }));
    }

    [Fact]
    public void UsarIntencionOpenCode_SinServidor_DebeFallar()
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        InvalidOperationException excepcion =
            Assert.Throws<InvalidOperationException>(() =>
                builder.UsarIntencionOpenCode(
                    PromptAgentePrueba,
                    "mensajeria-contexto",
                    configuracion =>
                        configuracion.AutenticacionBasica =
                            new ConfiguracionAutenticacionBasicaOpenCode(
                                "opencode",
                                "clave")));

        Assert.Contains(
            "servidor",
            excepcion.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsarIntencionOpenCode_SinAutenticacion_DebeFallar()
    {
        ServiceCollection servicios = new();
        ContextoMensajeriaBuilder builder = new(servicios);

        InvalidOperationException excepcion =
            Assert.Throws<InvalidOperationException>(() =>
                builder.UsarIntencionOpenCode(
                    PromptAgentePrueba,
                    "mensajeria-contexto",
                    configuracion =>
                        configuracion.Servidor =
                            new Uri("http://opencode:4096")));

        Assert.Contains(
            "autenticacion",
            excepcion.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsarIntencionOpenCode_DespuesDeOpenRouter_DebeReemplazarIntencion()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        ContextoMensajeriaBuilder builder = new(servicios);
        builder.UsarIntencionOpenRouter(
            "api-key-prueba",
            openRouter => openRouter.UsarMiniMax(
                PromptAgentePrueba));

        ConfigurarOpenCode(builder);

        using ServiceProvider serviceProvider =
            servicios.BuildServiceProvider();
        Assert.IsType<OpenCodeIntencionContextoServicio>(
            serviceProvider.GetRequiredService<
                IIntencionContextoConversacionServicio>());
    }

    [Fact]
    public void UsarIntencionPersonalizada_DespuesDeOpenCode_DebeReemplazarIntencion()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        ContextoMensajeriaBuilder builder = new(servicios);
        ConfigurarOpenCode(builder);

        builder.UsarIntencion<IntencionPersonalizadaPrueba>();

        using ServiceProvider serviceProvider =
            servicios.BuildServiceProvider();
        Assert.IsType<IntencionPersonalizadaPrueba>(
            serviceProvider.GetRequiredService<
                IIntencionContextoConversacionServicio>());
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
    public void AgregarWorkerMensajeria_DebeRegistrarComunicacionSingletonYWorkerUnaVez()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();

        servicios.AgregarMensajeria(builder =>
        {
            builder.AgregarWorkerMensajeria<ComunicacionMensajeriaPrueba>();
            builder.AgregarWorkerMensajeria<ComunicacionMensajeriaPrueba>();
        });

        using ServiceProvider proveedor = servicios.BuildServiceProvider();
        ComunicacionMensajeriaPrueba comunicacion = proveedor
            .GetRequiredService<ComunicacionMensajeriaPrueba>();
        IComunicacionMensajeriaAPI contrato = proveedor
            .GetRequiredService<IComunicacionMensajeriaAPI>();
        List<IHostedService> hostedServices = proveedor.GetServices<IHostedService>().ToList();

        Assert.Same(comunicacion, contrato);
        Assert.Single(hostedServices, servicio => servicio is MensajeriaWorker);
        Assert.Equal(
            ServiceLifetime.Singleton,
            ObtenerDescriptor(servicios, typeof(IMensajeServicio)).Lifetime);
    }

    [Fact]
    public void AgregarWorkerMensajeriaEntradaInfobip_DebeRegistrarSoloRecepcion()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();

        servicios.AgregarMensajeria(builder =>
            builder.AgregarWorkerMensajeriaEntradaInfobip());

        using ServiceProvider proveedor = servicios.BuildServiceProvider();
        ComunicacionInfobipServicio comunicacion = proveedor
            .GetRequiredService<ComunicacionInfobipServicio>();

        Assert.Same(
            comunicacion,
            proveedor.GetRequiredService<IRecepcionMensajeriaAPI>());
        Assert.Same(
            comunicacion,
            proveedor.GetRequiredService<IConfirmacionMensajeEntranteAPI>());
        Assert.Same(
            comunicacion,
            proveedor.GetRequiredService<IRecepcionWebhookInfobipAPI>());
        Assert.Empty(proveedor.GetServices<IEnvioMensajeriaAPI>());
        Assert.Empty(proveedor.GetServices<IInfobipWhatsAppCliente>());
        Assert.Single(
            proveedor.GetServices<IHostedService>(),
            servicio => servicio is MensajeriaWorker);
    }

    [Fact]
    public void AgregarWorkerMensajeriaInfobip_DebeRegistrarRecepcionEnvioYClienteTipado()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();

        servicios.AgregarMensajeria(builder =>
            builder.AgregarWorkerMensajeriaInfobip(
                new Uri("https://api.infobip.com"),
                "api-key-prueba",
                configuracion => configuracion.Timeout = TimeSpan.FromSeconds(18)));

        using ServiceProvider proveedor = servicios.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        ComunicacionInfobipServicio comunicacion = proveedor
            .GetRequiredService<ComunicacionInfobipServicio>();
        ConfiguracionClienteInfobip configuracion = proveedor
            .GetRequiredService<ConfiguracionClienteInfobip>();
        IInfobipWhatsAppCliente cliente = proveedor
            .GetRequiredService<IInfobipWhatsAppCliente>();

        Assert.Same(
            comunicacion,
            proveedor.GetRequiredService<IRecepcionMensajeriaAPI>());
        Assert.Same(
            comunicacion,
            proveedor.GetRequiredService<IEnvioMensajeriaAPI>());
        Assert.IsType<InfobipWhatsAppCliente>(cliente);
        Assert.IsType<AdaptadorMensajeSalidaInfobip>(
            proveedor.GetRequiredService<IAdaptadorMensajeSalidaInfobip>());
        Assert.Equal(TimeSpan.FromSeconds(18), configuracion.Timeout);
        Assert.Equal(
            ServiceLifetime.Scoped,
            ObtenerDescriptor(
                servicios,
                typeof(IRegistrarIntentoEnvioInfobipAplicacion)).Lifetime);
        Assert.Single(
            proveedor.GetServices<IHostedService>(),
            servicio => servicio is MensajeriaWorker);
    }

    [Fact]
    public void AgregarWorkerMensajeriaInfobip_ConSalidaPersonalizada_DebeFallar()
    {
        ServiceCollection servicios = new();
        servicios.AddLogging();
        servicios.AgregarMensajeria(builder =>
            builder.AgregarWorkerMensajeria<ComunicacionMensajeriaPrueba>());
        MensajeriaBuilder builder = new(servicios);

        InvalidOperationException excepcion = Assert.Throws<InvalidOperationException>(() =>
            builder.AgregarWorkerMensajeriaInfobip(
                new Uri("https://api.infobip.com"),
                "api-key-prueba"));

        Assert.Contains(
            "salida",
            excepcion.Message,
            StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(
            ServiceLifetime.Singleton,
            ObtenerDescriptor(servicios, typeof(IMensajeServicio)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            ObtenerDescriptor(servicios, typeof(IColaEventosMensajeriaEntradaServicio)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            ObtenerDescriptor(servicios, typeof(IColaEventosMensajeriaSalidaServicio)).Lifetime);

        Type[] aplicacionesScoped =
        [
            typeof(ICargarEventosMensajeriaPendientesAplicacion),
            typeof(ICargarEventosMensajeriaSalidaPendientesAplicacion),
            typeof(IRegistrarMensajeEntranteAplicacion),
            typeof(IRegistrarMensajeSalidaAplicacion),
            typeof(IObtenerMensajeSalidaPendienteAplicacion),
            typeof(IRegistrarResultadoEnvioMensajeAplicacion),
            typeof(IOrquestarMensajeContextoAplicacion),
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
        IMensajeServicio primerMensajeServicio = serviceProvider.GetRequiredService<IMensajeServicio>();
        IMensajeServicio segundoMensajeServicio = serviceProvider.GetRequiredService<IMensajeServicio>();

        Assert.Same(primerOrquestador, segundoOrquestador);
        Assert.Same(primeraFactory, segundaFactory);
        Assert.Same(primerMensajeServicio, segundoMensajeServicio);
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
    public void AgregarMensajeria_DebeRegistrarConfiguracionAgrupacionPredeterminada()
    {
        ServiceCollection servicios = new();

        servicios.AgregarMensajeria(_ => { });

        ServiceDescriptor descriptor = ObtenerDescriptor(
            servicios,
            typeof(ConfiguracionAgrupacionMensajesEntrada));
        ConfiguracionAgrupacionMensajesEntrada configuracion =
            Assert.IsType<ConfiguracionAgrupacionMensajesEntrada>(
                descriptor.ImplementationInstance);

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(TimeSpan.FromSeconds(2), configuracion.TiempoInactividad);
        Assert.Equal(10, configuracion.CantidadMaximaMensajesPorLote);
    }

    [Fact]
    public void ConfigurarAgrupacionMensajesEntrada_DebeReemplazarConfiguracionPredeterminada()
    {
        ServiceCollection servicios = new();
        ConfiguracionAgrupacionMensajesEntrada configuracionEsperada = new()
        {
            TiempoInactividad = TimeSpan.FromMilliseconds(750),
            CantidadMaximaMensajesPorLote = 4
        };

        servicios.AgregarMensajeria(builder =>
            builder.ConfigurarAgrupacionMensajesEntrada(configuracionEsperada));

        ServiceDescriptor descriptor = ObtenerDescriptor(
            servicios,
            typeof(ConfiguracionAgrupacionMensajesEntrada));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(configuracionEsperada, descriptor.ImplementationInstance);
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

    private static void ConfigurarOpenCode(
        ContextoMensajeriaBuilder builder)
    {
        builder.UsarIntencionOpenCode(
            PromptAgentePrueba,
            "mensajeria-contexto",
            configuracion =>
            {
                configuracion.Servidor =
                    new Uri("http://opencode:4096");
                configuracion.AutenticacionBasica =
                    new ConfiguracionAutenticacionBasicaOpenCode(
                        "opencode",
                        "clave-secreta");
            });
    }

    private static HttpClient ObtenerHttpClient(
        OpenCodeCliente cliente)
    {
        FieldInfo campo = typeof(OpenCodeCliente).GetField(
            "httpClient",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "No se encontro el HttpClient tipado de OpenCode.");
        return (HttpClient)campo.GetValue(cliente)!;
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

    private sealed class ComunicacionMensajeriaPrueba : IComunicacionMensajeriaAPI
    {
        public Task<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
            DTOEnvioMensajePendiente mensaje,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
