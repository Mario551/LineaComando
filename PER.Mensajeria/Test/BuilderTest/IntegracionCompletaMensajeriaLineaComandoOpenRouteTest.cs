using BuilderTest.Infraestructura;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Builder;
using PER.Comandos.LineaComandos.BuilderInicializador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Registro;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Datos.Configuracion;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Servicio.Mensaje;
using Xunit.Abstractions;

namespace BuilderTest;

public class IntegracionCompletaMensajeriaLineaComandoOpenRouteTest
{
    private readonly ITestOutputHelper output;

    public IntegracionCompletaMensajeriaLineaComandoOpenRouteTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    private const string CodigoComando = "pedido consultar";
    private const string Pedido = "54013";
    private const string EstadoPedido = "despachado";
    private const string ModeloOpenRoute = "minimax/minimax-m3";
    private static readonly TimeSpan TiempoEsperaFlujo = TimeSpan.FromMinutes(10);

    public static IEnumerable<object[]> Motores
    {
        get
        {
            yield return new object[] { MotorIntegracionCompletaPrueba.PostgreSql };
            yield return new object[] { MotorIntegracionCompletaPrueba.SqlServer };
        }
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task FlujoCompleto_BuilderLineaComandoMensajeriaOpenRoute_DebeRegistrarSalida(MotorIntegracionCompletaPrueba motor)
    {
        string apiKey = LeerVariableObligatoria(
            "OPENROUTE_MENSAJERIA",
            "La variable de entorno OPENROUTE_MENSAJERIA es obligatoria para el test de integración real con OpenRouter.");
        ConfiguracionBaseDatosPrueba baseDatos = CrearConfiguracionBaseDatos(motor);
        RegistroIntegracionMensajeriaOpenRoutePrueba registro = new();
        RegistroLoggerPrueba registroLogger = new(output);
        ServiceCollection servicios = new();
        servicios.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new LoggerProviderPrueba(registroLogger));
        });
        string directorioOpenRouter = CrearDirectorioOpenRouterPrueba();
        output.WriteLine($"Archivos OpenRouter: {directorioOpenRouter}");
        servicios.AddSingleton(registro);
        servicios.AddSingleton(new ConfiguracionOpenRouteMensajeriaPrueba(apiKey, directorioOpenRouter));

        LineaComandoBuilder lineaComandoBuilder = servicios.AddLineaComando(async (serviceProvider, builderInicializador, cancellationToken) =>
        {
            await RegistrarComandosPruebaAsync(serviceProvider, builderInicializador, cancellationToken);
        });

        ConfigurarBaseDatos(lineaComandoBuilder, baseDatos);

        lineaComandoBuilder.AgregarMensajeria(builder => builder
            .ConfigurarLineaConversacion(TimeSpan.FromHours(24))
            .ConfigurarContextoConversacion(new ConfiguracionContextoConversacion { MaximoIteraciones = 4 })
            .ConfigurarContexto(contexto => contexto
                .AgregarFiltro<PrimerFiltroContextoPrueba>()
                .AgregarFiltro<SegundoFiltroContextoPrueba>()
                .UsarCatalogoComandos<CatalogoComandosLineaComandoPrueba>()
                .UsarIntencion<IntencionOpenRouteMensajeriaPrueba>()
                .UsarEjecutorComandos<EjecutorComandoColaLineaComandoPrueba>()
                .UsarProveedorHistorial<ProveedorHistorialContextoPrueba>())
            .AgregarWorkerOrquestador());

        ReconfigurarMensajeriaContextoDBParaEsquemaPrueba(servicios, baseDatos);
        lineaComandoBuilder.Build();

        await using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        await serviceProvider.InicializarLineaComandoAsync();
        await CrearCuentaCanalAsync(serviceProvider, baseDatos.CuentaCanal);

        List<IHostedService> hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        CancellationTokenSource timeoutFlujo = new(TiempoEsperaFlujo);

        try
        {
            await IniciarHostedServicesAsync(hostedServices, timeoutFlujo.Token);

            DTORegistrarMensajeEntranteRespuesta respuestaEntrada;
            using (IServiceScope alcance = serviceProvider.CreateScope())
            {
                IMensajeServicio mensajeServicio = alcance.ServiceProvider.GetRequiredService<IMensajeServicio>();
                respuestaEntrada = await mensajeServicio.RecibirAsync(CrearSolicitudEntrada(baseDatos.CuentaCanal), timeoutFlujo.Token);
            }

            ILogger<IntegracionCompletaMensajeriaLineaComandoOpenRouteTest> logger = serviceProvider
                .GetRequiredService<ILogger<IntegracionCompletaMensajeriaLineaComandoOpenRouteTest>>();
            logger.LogInformation(
                "Mensaje entrante registrado. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDMensaje={IDMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                respuestaEntrada.IDProcesamientoInternoMensaje,
                respuestaEntrada.IDMensaje,
                respuestaEntrada.IDConversacion,
                respuestaEntrada.IDLineaConversacion);

            ResultadoFlujoCompletoPrueba resultado = await EsperarProcesamientoAsync(
                serviceProvider,
                respuestaEntrada.IDProcesamientoInternoMensaje,
                logger,
                timeoutFlujo.Token);

            Assert.True(respuestaEntrada.Registrado);
            Assert.Equal("procesado", resultado.Procesamiento.IDEstadoProcesamientoInternoMensaje);
            Assert.NotNull(resultado.Procesamiento.FechaProcesado);
            Assert.Null(resultado.Procesamiento.Error);
            Assert.Single(resultado.MensajesEntrada);
            Assert.NotEmpty(resultado.MensajesSalida);
            Assert.NotEmpty(resultado.EnviosPendientes);
            Assert.Equal(3, resultado.MetadataIA.Count);
            Assert.Equal(
                [nameof(AccionContextoTipo.Comando), nameof(AccionContextoTipo.Historial), nameof(AccionContextoTipo.Responder)],
                resultado.MetadataIA.OrderBy(metadata => metadata.Iteracion).Select(metadata => metadata.AccionDecidida));
            Assert.Equal(
                [
                    ("user", "mensaje_entrada"),
                    ("assistant", "decision_comando"),
                    ("tool", "resultado_comando"),
                    ("assistant", "decision_historial"),
                    ("tool", "resultado_historial"),
                    ("assistant", "respuesta_final")
                ],
                resultado.EntradasContextoIA
                    .OrderBy(entrada => entrada.Orden)
                    .Select(entrada => (entrada.IDRolContextoIA, entrada.IDTipoEntradaContextoIA)));
            Assert.Equal([1, 2, 3, 4, 5, 6], resultado.EntradasContextoIA.OrderBy(entrada => entrada.Orden).Select(entrada => entrada.Orden));
            Assert.All(
                resultado.EntradasContextoIA.Where(entrada => entrada.IDRolContextoIA == "assistant"),
                entrada => Assert.NotNull(entrada.IDMetadataRazonamientoIA));
            Assert.All(resultado.EntradasContextoIA, entrada => Assert.NotEqual(default, entrada.FechaEntrada));

            DAOMensaje mensajeSalida = Assert.Single(resultado.MensajesSalida);
            Assert.Contains(Pedido, mensajeSalida.Contenido ?? string.Empty);
            Assert.Contains(EstadoPedido, mensajeSalida.Contenido ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("historial", mensajeSalida.Contenido ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            Assert.Contains(registro.CatalogosIA, comandos => comandos.Any(comando => comando.Codigo == CodigoComando));
            Assert.Contains(registro.ComandosEncolados, comando => comando.Codigo == CodigoComando && comando.Parametros.TryGetValue("pedido", out string? pedido) && pedido == Pedido);
            Assert.Contains(registro.ComandosEjecutados, comando => comando.Pedido == Pedido && comando.Estado == EstadoPedido);
            Assert.Contains(registro.DatosIntermediosIA, datos => datos.Any(dato => dato.Tipo == "comando" && (dato.Contenido ?? string.Empty).Contains(Pedido)));
            Assert.Contains(registro.DatosIntermediosIA, datos => datos.Any(dato => dato.Tipo == "historial" && (dato.Contenido ?? string.Empty).Contains("Historial de prueba")));
            Assert.Contains(registro.HistorialesSolicitados, historial => historial.IDConversacion > 0);
            Assert.Equal(3, registro.EntradasContextoIA.Count);
            Assert.Equal([1, 3, 5], registro.EntradasContextoIA.Select(entradas => entradas.Count));
            EntradaContextoIA decisionComandoReenviada = Assert.Single(
                registro.EntradasContextoIA[1],
                entrada => entrada.IDTipoEntradaContextoIA == "decision_comando");
            Assert.NotNull(decisionComandoReenviada.Metadata);
            Assert.Equal("OpenRouter", decisionComandoReenviada.Metadata.Proveedor);

            int indiceComandoEjecutado = registro.Operaciones.IndexOf("comando_ejecutado");
            int indiceHistorialSolicitado = registro.Operaciones.IndexOf("historial_solicitado");
            Assert.True(indiceComandoEjecutado >= 0, "El comando de prueba debio ejecutarse antes de pedir historial.");
            Assert.True(indiceHistorialSolicitado > indiceComandoEjecutado, "El historial debe pedirse despues del resultado del comando.");

            List<RegistroFiltroPrueba> filtrosPrimeraIteracion = registro.Filtros
                .Where(filtro => filtro.Iteracion == 1)
                .ToList();
            Assert.Equal(["primer_filtro", "segundo_filtro"], filtrosPrimeraIteracion.Select(filtro => filtro.Nombre).ToList());
            Assert.True(registro.Filtros.Select(filtro => filtro.Iteracion).Distinct().Count() >= 3);
            registroLogger.AssertSinErrores();
        }
        catch (OperationCanceledException) when (timeoutFlujo.IsCancellationRequested)
        {
            throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 2 minutos.");
        }
        finally
        {
            using CancellationTokenSource timeoutApagado = new(TimeSpan.FromSeconds(10));
            await DetenerHostedServicesAsync(hostedServices, timeoutApagado.Token);
            timeoutFlujo.Dispose();
        }
    }

    private static async Task RegistrarComandosPruebaAsync(
        IServiceProvider serviceProvider,
        IBuilderInicializador builderInicializador,
        CancellationToken cancellationToken)
    {
        RegistroIntegracionMensajeriaOpenRoutePrueba registro = serviceProvider.GetRequiredService<RegistroIntegracionMensajeriaOpenRoutePrueba>();

        await builderInicializador
            .NewBuilderComando()
            .Argumentos(CodigoComando, "Consulta el estado de un pedido de prueba")
            .Accion(new ConsultarPedidoComando(registro))
            .RegistrarAsync();
    }

    private static void ConfigurarBaseDatos(LineaComandoBuilder builder, ConfiguracionBaseDatosPrueba baseDatos)
    {
        if (baseDatos.Motor == MotorIntegracionCompletaPrueba.PostgreSql)
        {
            builder.UsePostgresql(baseDatos.ConnectionStringBase, baseDatos.Esquema);
            return;
        }

        builder.UseSqlServer(baseDatos.ConnectionStringBase, baseDatos.Esquema);
    }

    private static void ReconfigurarMensajeriaContextoDBParaEsquemaPrueba(IServiceCollection servicios, ConfiguracionBaseDatosPrueba baseDatos)
    {
        for (int indice = servicios.Count - 1; indice >= 0; indice--)
        {
            Type tipoServicio = servicios[indice].ServiceType;
            if (tipoServicio == typeof(MensajeriaContextoDB)
                || tipoServicio == typeof(DbContextOptions<MensajeriaContextoDB>)
                || tipoServicio == typeof(DbContextOptions))
            {
                servicios.RemoveAt(indice);
            }
        }

        servicios.AddDbContext<MensajeriaContextoDB>(opciones =>
        {
            opciones.ReplaceService<IModelCacheKeyFactory, ModeloCachePorContextoIntegracionPrueba>();

            if (baseDatos.Motor == MotorIntegracionCompletaPrueba.PostgreSql)
            {
                NpgsqlConnectionStringBuilder builderConexion = new(baseDatos.ConnectionStringBase)
                {
                    SearchPath = baseDatos.Esquema
                };
                opciones.UseNpgsql(builderConexion.ConnectionString);
                return;
            }

            opciones.UseSqlServer(baseDatos.ConnectionStringBase);
        });
    }

    private static async Task CrearCuentaCanalAsync(IServiceProvider serviceProvider, string cuenta)
    {
        using IServiceScope alcance = serviceProvider.CreateScope();
        MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
        DAOCanalComunicacion canal = await contexto.CanalesComunicacion.SingleAsync(canalActual => canalActual.Canal == "whatsapp");
        DAOCuentaCanal cuentaCanal = new()
        {
            IDCanalComunicacion = canal.ID,
            Cuenta = cuenta,
            Descripcion = $"Cuenta {cuenta}",
            Activa = true
        };

        contexto.CuentasCanal.Add(cuentaCanal);
        await contexto.SaveChangesAsync();
    }

    private static async Task IniciarHostedServicesAsync(IEnumerable<IHostedService> hostedServices, CancellationToken cancellationToken)
    {
        foreach (IHostedService hostedService in hostedServices)
        {
            await hostedService.StartAsync(cancellationToken);
        }
    }

    private static async Task DetenerHostedServicesAsync(IReadOnlyList<IHostedService> hostedServices, CancellationToken cancellationToken)
    {
        for (int indice = hostedServices.Count - 1; indice >= 0; indice--)
        {
            try
            {
                await hostedServices[indice].StopAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static DTORegistrarMensajeEntranteSolicitud CrearSolicitudEntrada(string cuenta)
    {
        return new DTORegistrarMensajeEntranteSolicitud
        {
            Mensaje = new DTOMensajeEntrante
            {
                Canal = "whatsapp",
                Cuenta = cuenta,
                IdentificadorParticipante = "3001234567",
                TipoParticipante = "telefono",
                TipoMensaje = "texto",
                TelefonoOrigen = "3001234567",
                TelefonoDestino = "6011234567",
                Contenido = $"Consulta el pedido {Pedido}",
                IdentificadorExternoMensaje = $"openroute_{Guid.NewGuid():N}",
                FechaMensaje = DateTime.Now
            }
        };
    }

    private static async Task<ResultadoFlujoCompletoPrueba> EsperarProcesamientoAsync(
        IServiceProvider serviceProvider,
        long idProcesamientoInternoMensaje,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
            using IServiceScope alcance = serviceProvider.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
            DAOProcesamientoInternoMensaje procesamiento = await contexto.ProcesamientosInternosMensaje.SingleAsync(
                procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje,
                cancellationToken);
            List<DAOMensaje> mensajesEntrada = await contexto.Mensajes
                .Where(mensaje => mensaje.IDDireccionMensaje == "entrada")
                .ToListAsync(cancellationToken);
            List<DAOMensaje> mensajesSalida = await contexto.Mensajes
                .Where(mensaje => mensaje.IDDireccionMensaje == "salida")
                .ToListAsync(cancellationToken);
            List<DAOEnvioMensaje> enviosPendientes = await contexto.EnviosMensaje
                .Where(envio => envio.IDEstadoEnvioMensaje == "pendiente")
                .ToListAsync(cancellationToken);
            List<DAOMetadataRazonamientoIALineaConversacion> metadataIA = await contexto.MetadataRazonamientoIALineaConversacion
                .AsNoTracking()
                .Where(metadata => metadata.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .OrderBy(metadata => metadata.Iteracion)
                .ToListAsync(cancellationToken);
            List<DAOEntradaContextoIA> entradasContextoIA = await contexto.EntradasContextoIA
                .AsNoTracking()
                .Where(entrada => entrada.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .OrderBy(entrada => entrada.Orden)
                .ToListAsync(cancellationToken);

            if (procesamiento.IDEstadoProcesamientoInternoMensaje == "error")
            {
                throw new InvalidOperationException($"El procesamiento quedó en error: {procesamiento.Error}");
            }

            if (procesamiento.IDEstadoProcesamientoInternoMensaje == "procesado" && mensajesSalida.Count > 0)
            {
                return new ResultadoFlujoCompletoPrueba(
                    procesamiento,
                    mensajesEntrada,
                    mensajesSalida,
                    enviosPendientes,
                    metadataIA,
                    entradasContextoIA);
            }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarEstadoTimeoutAsync(serviceProvider, idProcesamientoInternoMensaje, logger);
            throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 2 minutos.");
        }

        await RegistrarEstadoTimeoutAsync(serviceProvider, idProcesamientoInternoMensaje, logger);
        throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 2 minutos.");
    }

    private static async Task RegistrarEstadoTimeoutAsync(
        IServiceProvider serviceProvider,
        long idProcesamientoInternoMensaje,
        ILogger logger)
    {
        try
        {
            using IServiceScope alcance = serviceProvider.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
            DAOProcesamientoInternoMensaje? procesamiento = await contexto.ProcesamientosInternosMensaje
                .AsNoTracking()
                .SingleOrDefaultAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje);
            int mensajesEntrada = await contexto.Mensajes.AsNoTracking().CountAsync(mensaje => mensaje.IDDireccionMensaje == "entrada");
            int mensajesSalida = await contexto.Mensajes.AsNoTracking().CountAsync(mensaje => mensaje.IDDireccionMensaje == "salida");
            int enviosPendientes = await contexto.EnviosMensaje.AsNoTracking().CountAsync(envio => envio.IDEstadoEnvioMensaje == "pendiente");

            logger.LogError(
                "Timeout esperando flujo completo. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, Estado={Estado}, Intentos={Intentos}, Error={Error}, MensajesEntrada={MensajesEntrada}, MensajesSalida={MensajesSalida}, EnviosPendientes={EnviosPendientes}",
                idProcesamientoInternoMensaje,
                procesamiento?.IDEstadoProcesamientoInternoMensaje,
                procesamiento?.Intentos,
                procesamiento?.Error,
                mensajesEntrada,
                mensajesSalida,
                enviosPendientes);
        }
        catch (Exception excepcion)
        {
            logger.LogError(excepcion, "No se pudo registrar el estado final despues del timeout del flujo completo.");
        }
    }

    private static ConfiguracionBaseDatosPrueba CrearConfiguracionBaseDatos(MotorIntegracionCompletaPrueba motor)
    {
        if (motor == MotorIntegracionCompletaPrueba.PostgreSql)
        {
            return new ConfiguracionBaseDatosPrueba(
                motor,
                LeerVariableObligatoria(
                    "MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL",
                    "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_POSTGRESQL es obligatoria para el test completo con PostgreSQL."),
                $"test_mensajeria_full_{Guid.NewGuid():N}",
                $"cuenta_full_{Guid.NewGuid():N}");
        }

        return new ConfiguracionBaseDatosPrueba(
            motor,
            LeerVariableObligatoria(
                "MENSAJERIA_COMANDOS_CONEXION_SQLSERVER",
                "La variable de entorno MENSAJERIA_COMANDOS_CONEXION_SQLSERVER es obligatoria para el test completo con SQL Server."),
            $"test_mensajeria_full_sql_{Guid.NewGuid():N}",
            $"cuenta_full_{Guid.NewGuid():N}");
    }

    private static string LeerVariableObligatoria(string nombre, string mensaje)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        Assert.False(string.IsNullOrWhiteSpace(valor), mensaje);
        return valor!;
    }

    private static string CrearDirectorioOpenRouterPrueba()
    {
        // Formato para buscar despues: /tmp/per_mensajeria_openrouter_yyyyMMdd_{Guid}
        string fecha = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string ruta = Path.Combine(Path.GetTempPath(), $"per_mensajeria_openrouter_{fecha}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    public enum MotorIntegracionCompletaPrueba
    {
        PostgreSql,
        SqlServer
    }

    private sealed record ConfiguracionBaseDatosPrueba(
        MotorIntegracionCompletaPrueba Motor,
        string ConnectionStringBase,
        string Esquema,
        string CuentaCanal);

    private sealed record ResultadoFlujoCompletoPrueba(
        DAOProcesamientoInternoMensaje Procesamiento,
        List<DAOMensaje> MensajesEntrada,
        List<DAOMensaje> MensajesSalida,
        List<DAOEnvioMensaje> EnviosPendientes,
        List<DAOMetadataRazonamientoIALineaConversacion> MetadataIA,
        List<DAOEntradaContextoIA> EntradasContextoIA);

    private sealed class ModeloCachePorContextoIntegracionPrueba : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            return (context.GetType(), context.ContextId.InstanceId, designTime);
        }
    }

    private sealed class RegistroIntegracionMensajeriaOpenRoutePrueba
    {
        private readonly object sync = new();

        public List<RegistroFiltroPrueba> Filtros { get; } = [];

        public List<IReadOnlyList<ComandoContexto>> CatalogosIA { get; } = [];

        public List<IReadOnlyList<DatoIntermedioContexto>> DatosIntermediosIA { get; } = [];

        public List<IReadOnlyList<EntradaContextoIA>> EntradasContextoIA { get; } = [];

        public List<RegistroComandoEncoladoPrueba> ComandosEncolados { get; } = [];

        public List<RegistroComandoEjecutadoPrueba> ComandosEjecutados { get; } = [];

        public List<RegistroHistorialSolicitadoPrueba> HistorialesSolicitados { get; } = [];

        public List<string> Operaciones { get; } = [];

        public void RegistrarFiltro(string nombre, int iteracion)
        {
            lock (sync)
            {
                Filtros.Add(new RegistroFiltroPrueba(nombre, iteracion));
            }
        }

        public void RegistrarLlamadaIA(
            IReadOnlyList<ComandoContexto> comandos,
            IReadOnlyList<DatoIntermedioContexto> datosIntermedios,
            IReadOnlyList<EntradaContextoIA> entradasContextoIA)
        {
            lock (sync)
            {
                CatalogosIA.Add(comandos.ToList());
                DatosIntermediosIA.Add(datosIntermedios.ToList());
                EntradasContextoIA.Add(entradasContextoIA.ToList());
            }
        }

        public void RegistrarComandoEncolado(string codigo, IReadOnlyDictionary<string, string> parametros)
        {
            lock (sync)
            {
                ComandosEncolados.Add(new RegistroComandoEncoladoPrueba(codigo, new Dictionary<string, string>(parametros)));
                Operaciones.Add("comando_encolado");
            }
        }

        public void RegistrarComandoEjecutado(string pedido, string estado)
        {
            lock (sync)
            {
                ComandosEjecutados.Add(new RegistroComandoEjecutadoPrueba(pedido, estado));
                Operaciones.Add("comando_ejecutado");
            }
        }

        public void RegistrarHistorialSolicitado(long idConversacion)
        {
            lock (sync)
            {
                HistorialesSolicitados.Add(new RegistroHistorialSolicitadoPrueba(idConversacion));
                Operaciones.Add("historial_solicitado");
            }
        }
    }

    private sealed record RegistroFiltroPrueba(string Nombre, int Iteracion);

    private sealed record RegistroComandoEncoladoPrueba(string Codigo, Dictionary<string, string> Parametros);

    private sealed record RegistroComandoEjecutadoPrueba(string Pedido, string Estado);

    private sealed record RegistroHistorialSolicitadoPrueba(long IDConversacion);

    private sealed class PrimerFiltroContextoPrueba : IFiltroContextoConversacion
    {
        private readonly RegistroIntegracionMensajeriaOpenRoutePrueba registro;

        public PrimerFiltroContextoPrueba(RegistroIntegracionMensajeriaOpenRoutePrueba registro)
        {
            this.registro = registro;
        }

        public Task<ResultadoFiltroContexto> EjecutarAsync(EstadoIteracionContextoConversacion estado, CancellationToken cancellationToken)
        {
            registro.RegistrarFiltro("primer_filtro", estado.Iteracion);
            return Task.FromResult(ResultadoFiltroContexto.ContinuarFlujo());
        }
    }

    private sealed class SegundoFiltroContextoPrueba : IFiltroContextoConversacion
    {
        private readonly RegistroIntegracionMensajeriaOpenRoutePrueba registro;

        public SegundoFiltroContextoPrueba(RegistroIntegracionMensajeriaOpenRoutePrueba registro)
        {
            this.registro = registro;
        }

        public Task<ResultadoFiltroContexto> EjecutarAsync(EstadoIteracionContextoConversacion estado, CancellationToken cancellationToken)
        {
            registro.RegistrarFiltro("segundo_filtro", estado.Iteracion);
            return Task.FromResult(ResultadoFiltroContexto.ContinuarFlujo());
        }
    }

    private sealed class CatalogoComandosLineaComandoPrueba : IProveedorCatalogoComandoContextoServicio
    {
        private readonly IRegistroComandos<string, ResultadoComando> registroComandos;

        public CatalogoComandosLineaComandoPrueba(IRegistroComandos<string, ResultadoComando> registroComandos)
        {
            this.registroComandos = registroComandos;
        }

        public async Task<IReadOnlyList<ComandoContexto>> ObtenerAsync(
            SolicitudContextoConversacion solicitud,
            CancellationToken cancellationToken)
        {
            IEnumerable<MetadatosComando> comandos = await registroComandos.ObtenerComandosRegistradosAsync(cancellationToken);
            return comandos
                .Where(comando => comando.Activo)
                .Select(comando => new ComandoContexto
                {
                    Codigo = comando.RutaComando,
                    Descripcion = comando.Descripcion ?? string.Empty,
                    Alcance = "Prueba de integracion de pedido",
                    ReglasUso = "Usar solamente si el usuario pide consultar un pedido por numero.",
                    Autorizado = true,
                    Parametros = new Dictionary<string, string>
                    {
                        ["pedido"] = "Numero de pedido a consultar"
                    }
                })
                .ToList();
        }
    }

    private sealed class EjecutorComandoColaLineaComandoPrueba : IEjecutorComandoContextoServicio
    {
        private readonly IColaComandosMemoria colaComandosMemoria;
        private readonly RegistroIntegracionMensajeriaOpenRoutePrueba registro;

        public EjecutorComandoColaLineaComandoPrueba(
            IColaComandosMemoria colaComandosMemoria,
            RegistroIntegracionMensajeriaOpenRoutePrueba registro)
        {
            this.colaComandosMemoria = colaComandosMemoria;
            this.registro = registro;
        }

        public async Task<ResultadoComandoContexto> EjecutarAsync(
            SolicitudEjecutarComandoContexto solicitud,
            CancellationToken cancellationToken)
        {
            registro.RegistrarComandoEncolado(solicitud.Comando.Codigo, solicitud.Parametros);
            string datosComando = JsonSerializer.Serialize(solicitud.Parametros);
            ComandoEncolado comandoEncolado = await colaComandosMemoria.EncolarAsync(
                new SolicitudComando
                {
                    RutaComando = solicitud.Comando.Codigo,
                    Argumentos = string.Empty,
                    DatosDeComando = datosComando
                },
                cancellationToken);

            ResultadoComando resultado = await comandoEncolado.Resultado.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (!resultado.Exitoso)
            {
                return ResultadoComandoContexto.Fallo(resultado.MensajeError ?? "El comando fallo sin mensaje de error.");
            }

            string contenido = resultado.Salida is string salidaTexto
                ? salidaTexto
                : JsonSerializer.Serialize(resultado.Salida);
            return ResultadoComandoContexto.Exito(contenido);
        }
    }

    private sealed class ProveedorHistorialContextoPrueba : IProveedorHistorialContextoServicio
    {
        private readonly RegistroIntegracionMensajeriaOpenRoutePrueba registro;

        public ProveedorHistorialContextoPrueba(RegistroIntegracionMensajeriaOpenRoutePrueba registro)
        {
            this.registro = registro;
        }

        public Task<ResultadoHistorialContexto> ObtenerAsync(
            SolicitudContextoConversacion solicitud,
            CancellationToken cancellationToken)
        {
            registro.RegistrarHistorialSolicitado(solicitud.IDConversacion);
            return Task.FromResult(ResultadoHistorialContexto.Exito($"Historial de prueba: el pedido {Pedido} ya tenia seguimiento previo por mensajeria."));
        }
    }

    private sealed record ConfiguracionOpenRouteMensajeriaPrueba(string ApiKey, string DirectorioArchivos);

    private sealed class IntencionOpenRouteMensajeriaPrueba : IIntencionContextoConversacionServicio
    {
        private readonly string apiKey;
        private readonly string directorioArchivos;
        private readonly RegistroIntegracionMensajeriaOpenRoutePrueba registro;
        private readonly ILogger<IntencionOpenRouteMensajeriaPrueba> logger;

        public IntencionOpenRouteMensajeriaPrueba(
            ConfiguracionOpenRouteMensajeriaPrueba configuracion,
            RegistroIntegracionMensajeriaOpenRoutePrueba registro,
            ILogger<IntencionOpenRouteMensajeriaPrueba> logger)
        {
            apiKey = configuracion.ApiKey;
            directorioArchivos = configuracion.DirectorioArchivos;
            this.registro = registro;
            this.logger = logger;
        }

        public async Task<ResultadoIntencionContexto> DecidirAsync(
            SolicitudIntencionContexto solicitud,
            CancellationToken cancellationToken)
        {
            registro.RegistrarLlamadaIA(
                solicitud.Comandos,
                solicitud.DatosIntermedios,
                solicitud.EntradasContextoIA);
            ResultadoOpenRouteDecisionPrueba respuesta = await SolicitarDecisionAsync(solicitud, cancellationToken);
            return MapearRespuesta(respuesta.Contenido, respuesta.Metadata);
        }

        public Task<ResultadoCompactacionIntencionContexto> CompactarAsync(
            SolicitudCompactacionIntencionContexto solicitud,
            CancellationToken cancellationToken)
        {
            MetadataRazonamientoIAContexto metadata = new()
            {
                Proveedor = "openrouter",
                Modelo = ModeloOpenRoute,
                Adaptador = nameof(IntencionOpenRouteMensajeriaPrueba),
                AccionDecidida = "Compactar",
                Iteracion = solicitud.Iteracion
            };

            return Task.FromResult(ResultadoCompactacionIntencionContexto.Fallo(
                "La compactacion no forma parte de este escenario de integracion.",
                metadata));
        }

        private async Task<ResultadoOpenRouteDecisionPrueba> SolicitarDecisionAsync(SolicitudIntencionContexto solicitud, CancellationToken cancellationToken)
        {
            using HttpClient cliente = new()
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            Dictionary<string, object?> cuerpo = new()
            {
                ["model"] = ModeloOpenRoute,
                ["temperature"] = 0,
                ["max_tokens"] = 10000,
                ["response_format"] = new Dictionary<string, string>
                {
                    ["type"] = "json_object"
                },
                ["provider"] = new Dictionary<string, object?>
                {
                    ["only"] = new[] { "minimax" },
                    ["allow_fallbacks"] = false,
                    ["require_parameters"] = true
                },
                ["session_id"] = $"{Path.GetFileName(directorioArchivos)}-{solicitud.Solicitud.IDProcesamientoInternoMensaje}",
                ["messages"] = new List<Dictionary<string, string>>
                {
                    new()
                    {
                        ["role"] = "system",
                        ["content"] = CrearPromptSistema()
                    },
                    new()
                    {
                        ["role"] = "user",
                        ["content"] = CrearPromptUsuario(solicitud)
                    }
                }
            };

            string json = JsonSerializer.Serialize(cuerpo);
            await GuardarArchivoAsync(solicitud.Iteracion, "request.json", FormatearJson(json), cancellationToken);
            logger.LogInformation("Solicitud enviada a OpenRouter: {SolicitudOpenRouter}", json);
            using StringContent contenido = new(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage respuesta = await cliente.PostAsync(
                "https://openrouter.ai/api/v1/chat/completions",
                contenido,
                cancellationToken);
            string cuerpoRespuesta = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            await GuardarArchivoAsync(solicitud.Iteracion, "response.json", FormatearJson(cuerpoRespuesta), cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogError(
                    "OpenRouter devolvio error HTTP. StatusCode={StatusCode}, Cuerpo={CuerpoRespuesta}",
                    (int)respuesta.StatusCode,
                    cuerpoRespuesta);
                throw new InvalidOperationException($"OpenRouter devolvio {(int)respuesta.StatusCode}: {cuerpoRespuesta}");
            }

            using JsonDocument documento = JsonDocument.Parse(cuerpoRespuesta);
            string? contenidoModelo = documento.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(contenidoModelo))
            {
                logger.LogError("OpenRouter no devolvio contenido de decision. ArchivoRespuesta={ArchivoRespuesta}, Cuerpo={CuerpoRespuesta}", CrearRutaArchivo(solicitud.Iteracion, "response.json"), cuerpoRespuesta);
                throw new InvalidOperationException("OpenRouter no devolvio contenido de decision.");
            }

            await GuardarArchivoAsync(solicitud.Iteracion, "content.txt", contenidoModelo, cancellationToken);
            logger.LogInformation("Contenido devuelto por OpenRouter: {ContenidoModelo}", contenidoModelo);
            string contenidoDecision = LimpiarJson(contenidoModelo);
            return new ResultadoOpenRouteDecisionPrueba(
                contenidoDecision,
                CrearMetadata(solicitud, documento.RootElement, json, cuerpoRespuesta, contenidoDecision));
        }

        private Task GuardarArchivoAsync(int iteracion, string nombreArchivo, string contenido, CancellationToken cancellationToken)
        {
            return File.WriteAllTextAsync(CrearRutaArchivo(iteracion, nombreArchivo), contenido, Encoding.UTF8, cancellationToken);
        }

        private string CrearRutaArchivo(int iteracion, string nombreArchivo)
        {
            // Formato para buscar despues: /tmp/per_mensajeria_openrouter_yyyyMMdd_{Guid}
            return Path.Combine(directorioArchivos, $"iteracion_{iteracion}_{nombreArchivo}");
        }

        private static string FormatearJson(string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
            {
                return contenido;
            }

            try
            {
                using JsonDocument documento = JsonDocument.Parse(contenido);
                return JsonSerializer.Serialize(documento.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException)
            {
                return contenido;
            }
        }

        private static string CrearPromptSistema()
        {
            return "Eres un motor de decision para una prueba de integracion de mensajeria. "
                + "Debes responder exclusivamente JSON valido, sin markdown. "
                + "Debes seguir estrictamente este orden: primero comando, luego historial, finalmente respuesta. "
                + "Si no hay datos intermedios de tipo comando, responde {\"accion\":\"comando\",\"codigoComando\":\"pedido consultar\",\"parametros\":{\"pedido\":\"54013\"}}. "
                + "Si hay dato intermedio de tipo comando y no hay dato intermedio de tipo historial, responde {\"accion\":\"historial\"}. "
                + "Si hay datos intermedios de tipo comando e historial, responde {\"accion\":\"responder\",\"contenido\":\"El pedido 54013 esta despachado y fue validado con historial de prueba.\"}. "
                + "No saltes pasos y no inventes otro comando.";
        }

        private static string CrearPromptUsuario(SolicitudIntencionContexto solicitud)
        {
            object datos = new
            {
                iteracion = solicitud.Iteracion,
                mensaje = solicitud.Solicitud.Contenido,
                fechaMensaje = solicitud.Solicitud.FechaMensaje,
                estadoContextoInicial = solicitud.EstadoContextoInicial,
                comandos = solicitud.Comandos.Select(comando => new
                {
                    codigo = comando.Codigo,
                    descripcion = comando.Descripcion,
                    parametros = comando.Parametros
                }),
                datosIntermedios = solicitud.DatosIntermedios.Select(dato => new
                {
                    tipo = dato.Tipo,
                    contenido = dato.Contenido
                }),
                entradasContextoIA = solicitud.EntradasContextoIA
                    .OrderBy(entrada => entrada.Orden)
                    .Select(entrada => new
                    {
                        orden = entrada.Orden,
                        rol = entrada.IDRolContextoIA,
                        tipo = entrada.IDTipoEntradaContextoIA,
                        contenido = entrada.Contenido,
                        toolCallID = entrada.ToolCallID,
                        fechaEntrada = entrada.FechaEntrada,
                        metadata = entrada.Metadata is null
                            ? null
                            : new
                            {
                                proveedor = entrada.Metadata.Proveedor,
                                modelo = entrada.Metadata.Modelo,
                                adaptador = entrada.Metadata.Adaptador,
                                iteracion = entrada.Metadata.Iteracion,
                                accion = entrada.Metadata.AccionDecidida,
                                finishReason = entrada.Metadata.FinishReason,
                                nativeFinishReason = entrada.Metadata.NativeFinishReason,
                                promptTokens = entrada.Metadata.PromptTokens,
                                completionTokens = entrada.Metadata.CompletionTokens,
                                reasoningTokens = entrada.Metadata.ReasoningTokens,
                                totalTokens = entrada.Metadata.TotalTokens,
                                content = entrada.Metadata.Content,
                                reasoning = entrada.Metadata.Reasoning,
                                reasoningDetails = entrada.Metadata.ReasoningDetailsJson
                            }
                    })
            };

            return JsonSerializer.Serialize(datos);
        }

        private static ResultadoIntencionContexto MapearRespuesta(
            string respuesta,
            MetadataRazonamientoIAContexto metadata)
        {
            using JsonDocument documento = JsonDocument.Parse(respuesta);
            JsonElement raiz = documento.RootElement;
            string accion = LeerString(raiz, "accion").ToLowerInvariant();
            metadata.AccionDecidida = accion;

            if (accion == "comando")
            {
                string codigoComando = LeerString(raiz, "codigoComando");
                Dictionary<string, string> parametros = LeerParametros(raiz);
                return ResultadoIntencionContexto.PedirComando(metadata, respuesta, codigoComando, parametros);
            }

            if (accion == "historial" || accion == "pedir_historial")
            {
                return ResultadoIntencionContexto.PedirHistorial(metadata, respuesta);
            }

            if (accion == "responder")
            {
                string contenido = LeerString(raiz, "contenido");
                return ResultadoIntencionContexto.Responder(
                    metadata,
                    respuesta,
                    new DTOMensajeSaliente
                    {
                        TipoMensaje = "texto",
                        Contenido = contenido,
                        FechaMensaje = DateTime.Now
                    });
            }

            if (accion is "no_responder" or "no responder")
            {
                return ResultadoIntencionContexto.NoResponder(metadata, respuesta);
            }

            if (accion == "error")
            {
                return ResultadoIntencionContexto.ConError(metadata, respuesta, LeerString(raiz, "error"));
            }

            throw new InvalidOperationException($"OpenRouter devolvio una accion no soportada: {accion}. Respuesta: {respuesta}");
        }

        private static MetadataRazonamientoIAContexto CrearMetadata(
            SolicitudIntencionContexto solicitud,
            JsonElement respuestaOpenRouter,
            string requestJson,
            string responseJson,
            string contenidoDecision)
        {
            JsonElement choice = respuestaOpenRouter.GetProperty("choices")[0];
            JsonElement message = choice.GetProperty("message");

            MetadataRazonamientoIAContexto metadata = new()
            {
                Proveedor = "OpenRouter",
                Modelo = ModeloOpenRoute,
                Adaptador = "PruebaOpenRouterMiniMaxM3",
                Iteracion = solicitud.Iteracion,
                FinishReason = LeerStringOpcional(choice, "finish_reason"),
                NativeFinishReason = LeerStringOpcional(choice, "native_finish_reason"),
                RequestJson = requestJson,
                ResponseJson = responseJson,
                Content = contenidoDecision,
                Reasoning = LeerStringOpcional(message, "reasoning"),
                ReasoningDetailsJson = LeerJsonOpcional(message, "reasoning_details")
            };

            if (respuestaOpenRouter.TryGetProperty("usage", out JsonElement usage))
            {
                metadata.PromptTokens = LeerEnteroOpcional(usage, "prompt_tokens");
                metadata.CompletionTokens = LeerEnteroOpcional(usage, "completion_tokens");
                metadata.TotalTokens = LeerEnteroOpcional(usage, "total_tokens");
                metadata.ReasoningTokens =
                    LeerEnteroOpcional(usage, "reasoning_tokens")
                    ?? LeerEnteroOpcional(usage, "reasoning");
            }

            return metadata;
        }

        private static string? LeerStringOpcional(JsonElement raiz, string propiedad)
        {
            if (raiz.TryGetProperty(propiedad, out JsonElement valor) && valor.ValueKind == JsonValueKind.String)
            {
                return valor.GetString();
            }

            return null;
        }

        private static int? LeerEnteroOpcional(JsonElement raiz, string propiedad)
        {
            if (raiz.TryGetProperty(propiedad, out JsonElement valor) && valor.ValueKind == JsonValueKind.Number)
            {
                return valor.GetInt32();
            }

            return null;
        }

        private static string? LeerJsonOpcional(JsonElement raiz, string propiedad)
        {
            if (raiz.TryGetProperty(propiedad, out JsonElement valor) && valor.ValueKind != JsonValueKind.Null)
            {
                return valor.GetRawText();
            }

            return null;
        }

        private static string LeerString(JsonElement raiz, string propiedad)
        {
            if (raiz.TryGetProperty(propiedad, out JsonElement valor) && valor.ValueKind == JsonValueKind.String)
            {
                return valor.GetString() ?? string.Empty;
            }

            throw new InvalidOperationException($"La respuesta de OpenRouter no contiene la propiedad string requerida '{propiedad}'.");
        }

        private static Dictionary<string, string> LeerParametros(JsonElement raiz)
        {
            Dictionary<string, string> parametros = [];
            if (!raiz.TryGetProperty("parametros", out JsonElement parametrosJson) || parametrosJson.ValueKind != JsonValueKind.Object)
            {
                return parametros;
            }

            foreach (JsonProperty propiedad in parametrosJson.EnumerateObject())
            {
                parametros[propiedad.Name] = propiedad.Value.ValueKind == JsonValueKind.String
                    ? propiedad.Value.GetString() ?? string.Empty
                    : propiedad.Value.ToString();
            }

            return parametros;
        }

        private sealed record ResultadoOpenRouteDecisionPrueba(
            string Contenido,
            MetadataRazonamientoIAContexto Metadata);

        private static string LimpiarJson(string contenido)
        {
            string limpio = contenido.Trim();
            if (limpio.StartsWith("{", StringComparison.Ordinal)
                && limpio.EndsWith("`", StringComparison.Ordinal))
            {
                limpio = limpio.TrimEnd('`').TrimEnd();
            }

            if (!limpio.StartsWith("```", StringComparison.Ordinal))
            {
                return limpio;
            }

            int primeraLinea = limpio.IndexOf('\n');
            int ultimaCerca = limpio.LastIndexOf("```", StringComparison.Ordinal);
            if (primeraLinea < 0 || ultimaCerca <= primeraLinea)
            {
                return limpio;
            }

            return limpio.Substring(primeraLinea + 1, ultimaCerca - primeraLinea - 1).Trim();
        }
    }

    private sealed class ConsultarPedidoComando : ComandoBase<string, ResultadoComando>
    {
        private readonly RegistroIntegracionMensajeriaOpenRoutePrueba registro;

        public ConsultarPedidoComando(RegistroIntegracionMensajeriaOpenRoutePrueba registro)
        {
            this.registro = registro;
        }

        public override void Preparar(ICollection<Parametro> parametros)
        {
        }

        public override Task<ResultadoComando> EjecutarAsync(string entrada, CancellationToken token = default)
        {
            using JsonDocument documento = JsonDocument.Parse(entrada);
            string pedido = documento.RootElement.GetProperty("pedido").GetString() ?? string.Empty;
            if (pedido != Pedido)
            {
                return Task.FromResult(ResultadoComando.Fallo($"Pedido de prueba inesperado: {pedido}"));
            }

            registro.RegistrarComandoEjecutado(pedido, EstadoPedido);
            return Task.FromResult(ResultadoComando.Exito($"Pedido {pedido}: {EstadoPedido}"));
        }
    }
}
