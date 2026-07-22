using BuilderTest.Infraestructura;
using System.Globalization;
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
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Registro;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;
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
    private const string PreferenciaAnterior = "entrega en la tarde";
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
        string promptAgente = CrearPromptAgenteIntegracionOpenRouter();
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
                .UsarIntencionOpenRouter(apiKey, openRouter => openRouter.UsarMiniMax(
                    promptAgente,
                    minimax =>
                {
                    minimax.MaximoTokens = 30000;
                    minimax.Temperatura = 0;
                }))
                .UsarEjecutorLineaComando())
            .AgregarWorkerOrquestador());

        ReconfigurarMensajeriaContextoDBParaEsquemaPrueba(servicios, baseDatos);
        lineaComandoBuilder.Build();

        await using ServiceProvider serviceProvider = servicios.BuildServiceProvider();
        await serviceProvider.InicializarLineaComandoAsync();
        await CrearCuentaCanalAsync(serviceProvider, baseDatos.CuentaCanal);
        CicloAnteriorPrueba cicloAnterior = await CrearCicloAnteriorAsync(
            serviceProvider,
            baseDatos.CuentaCanal);

        List<IHostedService> hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        CancellationTokenSource timeoutFlujo = new(TiempoEsperaFlujo);
        long? idProcesamientoInternoMensaje = null;

        try
        {
            await IniciarHostedServicesAsync(hostedServices, timeoutFlujo.Token);

            DTORegistrarMensajeEntranteRespuesta respuestaEntrada;
            using (IServiceScope alcance = serviceProvider.CreateScope())
            {
                IMensajeServicio mensajeServicio = alcance.ServiceProvider.GetRequiredService<IMensajeServicio>();
                respuestaEntrada = await mensajeServicio.RecibirAsync(CrearSolicitudEntrada(baseDatos.CuentaCanal), timeoutFlujo.Token);
            }
            idProcesamientoInternoMensaje = respuestaEntrada.IDProcesamientoInternoMensaje;

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
            await GuardarInformacionTecnicaOpenRouterAsync(
                directorioOpenRouter,
                resultado.InformacionTecnicaLlamadasIA,
                timeoutFlujo.Token);

            Assert.True(respuestaEntrada.Registrado);
            Assert.Equal("procesado", resultado.Procesamiento.IDEstadoProcesamientoInternoMensaje);
            Assert.NotNull(resultado.Procesamiento.FechaProcesado);
            Assert.Null(resultado.Procesamiento.Error);
            Assert.Equal(2, resultado.MensajesEntrada.Count);
            Assert.Contains(resultado.MensajesEntrada, mensaje => mensaje.ID == cicloAnterior.IDMensaje);
            Assert.Contains(resultado.MensajesEntrada, mensaje => mensaje.ID != cicloAnterior.IDMensaje && mensaje.Contenido?.Contains(Pedido) == true);
            Assert.NotEmpty(resultado.MensajesSalida);
            Assert.NotEmpty(resultado.EnviosPendientes);
            Assert.Equal(3, resultado.InformacionTecnicaLlamadasIA.Count);
            Assert.Equal(
                [nameof(AccionContextoTipo.Comando), nameof(AccionContextoTipo.ConsultarMensajesLineaAnterior), nameof(AccionContextoTipo.Responder)],
                resultado.InformacionTecnicaLlamadasIA.OrderBy(metadata => metadata.Iteracion).Select(metadata => metadata.AccionDecidida));
            Assert.All(resultado.InformacionTecnicaLlamadasIA, metadata => Assert.Equal(ModeloOpenRoute, metadata.Modelo));
            Assert.All(resultado.InformacionTecnicaLlamadasIA, metadata => Assert.Equal(nameof(MiniMaxOpenRouterAdaptador), metadata.Adaptador));
            Assert.All(
                resultado.InformacionTecnicaLlamadasIA,
                metadata => AssertRequestContienePromptAgente(metadata.RequestJson, promptAgente));
            Assert.Equal(
                [
                    ("user", "mensaje_entrada"),
                    ("assistant", "decision_comando"),
                    ("tool", "resultado_comando"),
                    ("assistant", "decision_consulta_mensajes_linea_anterior"),
                    ("tool", "resultado_consulta_mensajes_linea_anterior"),
                    ("assistant", "respuesta_final")
                ],
                resultado.MetadataEntradasContextoIA
                    .OrderBy(entrada => entrada.Orden)
                    .Select(entrada => (entrada.IDRolContextoIA, entrada.IDTipoEntradaContextoIA)));
            Assert.Equal([1, 2, 3, 4, 5, 6], resultado.MetadataEntradasContextoIA.OrderBy(entrada => entrada.Orden).Select(entrada => entrada.Orden));
            Assert.All(
                resultado.MetadataEntradasContextoIA.Where(entrada => entrada.IDRolContextoIA == "assistant"),
                entrada => Assert.NotNull(entrada.IDInformacionTecnicaLlamadaIA));
            Assert.All(resultado.MetadataEntradasContextoIA, entrada => Assert.NotEqual(default, entrada.FechaEntrada));
            DAOMetadataEntradaContextoIA decisionComando = resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA == "decision_comando");
            DAOMetadataEntradaContextoIA resultadoComando = resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA == "resultado_comando");
            DAOMetadataEntradaContextoIA decisionConsulta = resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA == "decision_consulta_mensajes_linea_anterior");
            DAOMetadataEntradaContextoIA resultadoConsulta = resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA == "resultado_consulta_mensajes_linea_anterior");
            Assert.False(string.IsNullOrWhiteSpace(decisionComando.ToolCallID));
            Assert.Equal(decisionComando.ToolCallID, resultadoComando.ToolCallID);
            Assert.False(string.IsNullOrWhiteSpace(decisionConsulta.ToolCallID));
            Assert.Equal(decisionConsulta.ToolCallID, resultadoConsulta.ToolCallID);
            using (JsonDocument referenciaConsulta = JsonDocument.Parse(resultadoConsulta.Contenido!))
            {
                Assert.Equal("cargada", referenciaConsulta.RootElement.GetProperty("estado").GetString());
                Assert.Equal(
                    cicloAnterior.IDLineaConversacion,
                    referenciaConsulta.RootElement.GetProperty("idLineaConversacion").GetInt64());
                Assert.Equal(
                    cicloAnterior.IDProcesamientoInternoMensaje,
                    referenciaConsulta.RootElement.GetProperty("idProcesamientoInternoMensaje").GetInt64());
                Assert.Equal(2, referenciaConsulta.RootElement.GetProperty("cantidadEntradas").GetInt32());
            }

            DAOMensaje mensajeSalida = Assert.Single(resultado.MensajesSalida);
            Assert.Contains(Pedido, mensajeSalida.Contenido ?? string.Empty);
            Assert.Contains(EstadoPedido, mensajeSalida.Contenido ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(PreferenciaAnterior, mensajeSalida.Contenido ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            DAOEjecucionComandoContexto ejecucionComando = Assert.Single(resultado.EjecucionesComandoContexto);
            Assert.Equal(CodigoComando, ejecucionComando.CodigoComando);
            Assert.Equal("completada", ejecucionComando.IDEstadoEjecucionComandoContexto);
            Assert.False(ejecucionComando.Activa);
            Assert.False(string.IsNullOrWhiteSpace(ejecucionComando.IdentificadorExterno));
            Assert.Contains(Pedido, ejecucionComando.ParametrosJson);
            Assert.Contains(registro.ComandosEjecutados, comando => comando.Pedido == Pedido && comando.Estado == EstadoPedido);
            int indiceComandoEjecutado = registro.Operaciones.IndexOf("comando_ejecutado");
            Assert.True(indiceComandoEjecutado >= 0, "El comando de prueba debio ejecutarse antes de consultar mensajes anteriores.");

            List<RegistroFiltroPrueba> filtrosPrimeraIteracion = registro.Filtros
                .Where(filtro => filtro.Iteracion == 1)
                .ToList();
            Assert.Equal(["primer_filtro", "segundo_filtro"], filtrosPrimeraIteracion.Select(filtro => filtro.Nombre).ToList());
            Assert.True(registro.Filtros.Select(filtro => filtro.Iteracion).Distinct().Count() >= 3);
            registroLogger.AssertSinErrores();
        }
        catch (OperationCanceledException) when (timeoutFlujo.IsCancellationRequested)
        {
            await GuardarInformacionTecnicaOpenRouterDisponibleAsync(
                serviceProvider,
                directorioOpenRouter,
                idProcesamientoInternoMensaje);
            throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 10 minutos.");
        }
        catch
        {
            await GuardarInformacionTecnicaOpenRouterDisponibleAsync(
                serviceProvider,
                directorioOpenRouter,
                idProcesamientoInternoMensaje);
            throw;
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
            .Resultado(new ProcesadorResultadoPedidoPrueba())
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

    private static async Task<CicloAnteriorPrueba> CrearCicloAnteriorAsync(
        IServiceProvider serviceProvider,
        string cuenta)
    {
        using IServiceScope alcance = serviceProvider.CreateScope();
        MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
        DAOCuentaCanal cuentaCanal = await contexto.CuentasCanal.SingleAsync(
            cuentaActual => cuentaActual.Cuenta == cuenta);
        DateTime fecha = DateTime.Now.AddDays(-2);
        DAOParticipanteConversacion participante = new()
        {
            IDTipoParticipanteConversacion = "telefono",
            IdentificadorParticipante = "3001234567"
        };
        contexto.ParticipantesConversacion.Add(participante);
        await contexto.SaveChangesAsync();

        DAOConversacion conversacion = new()
        {
            IDCuentaCanal = cuentaCanal.ID,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };
        contexto.Conversaciones.Add(conversacion);
        await contexto.SaveChangesAsync();
        contexto.ConversacionesParticipantes.Add(new DAOConversacionParticipante
        {
            IDConversacion = conversacion.ID,
            IDParticipanteConversacion = participante.ID,
            FechaUnion = fecha,
            Activo = true
        });

        DAOLineaConversacion linea = new()
        {
            IDConversacion = conversacion.ID,
            FechaInicio = fecha,
            FechaUltimaActividad = fecha.AddMinutes(2),
            Activa = false
        };
        contexto.LineasConversacion.Add(linea);
        await contexto.SaveChangesAsync();

        DAOMensaje mensaje = new()
        {
            IDLineaConversacion = linea.ID,
            IDTipoMensaje = "texto",
            IDDireccionMensaje = "entrada",
            TelefonoOrigen = "3001234567",
            TelefonoDestino = "6011234567",
            Contenido = $"Prefiero {PreferenciaAnterior}.",
            IdentificadorExternoMensaje = $"openroute_anterior_{Guid.NewGuid():N}",
            FechaMensaje = fecha,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };
        contexto.Mensajes.Add(mensaje);
        await contexto.SaveChangesAsync();

        DAOProcesamientoInternoMensaje procesamiento = new()
        {
            IDMensaje = mensaje.ID,
            IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
            IDEstadoProcesamientoInternoMensaje = "procesado",
            Intentos = 1,
            FechaCreacion = fecha,
            FechaProcesado = fecha.AddMinutes(2)
        };
        contexto.ProcesamientosInternosMensaje.Add(procesamiento);
        await contexto.SaveChangesAsync();

        DAOInformacionTecnicaLlamadaIALineaConversacion informacionTecnica = new()
        {
            IDLineaConversacion = linea.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMensaje = mensaje.ID,
            Proveedor = "prueba_preparacion",
            Modelo = "prueba_preparacion",
            Adaptador = "prueba_preparacion",
            Iteracion = 1,
            AccionDecidida = nameof(AccionContextoTipo.Responder),
            FinishReason = "stop",
            Content = "Preferencia registrada.",
            FechaCreacion = fecha.AddMinutes(1)
        };
        contexto.InformacionTecnicaLlamadasIALineaConversacion.Add(informacionTecnica);
        await contexto.SaveChangesAsync();
        contexto.MetadataEntradasContextoIA.AddRange(
            new DAOMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                Orden = 1,
                IDRolContextoIA = "user",
                IDTipoEntradaContextoIA = "mensaje_entrada",
                Contenido = mensaje.Contenido,
                FechaEntrada = mensaje.FechaMensaje,
                FechaCreacion = fecha
            },
            new DAOMetadataEntradaContextoIA
            {
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDInformacionTecnicaLlamadaIA = informacionTecnica.ID,
                Orden = 2,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "respuesta_final",
                Contenido = "Preferencia registrada.",
                FechaEntrada = fecha.AddMinutes(1),
                FechaCreacion = fecha.AddMinutes(1)
            });
        await contexto.SaveChangesAsync();

        return new CicloAnteriorPrueba(linea.ID, mensaje.ID, procesamiento.ID);
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
            List<DAOInformacionTecnicaLlamadaIALineaConversacion> informacionTecnicaLlamadasIA = await contexto.InformacionTecnicaLlamadasIALineaConversacion
                .AsNoTracking()
                .Where(metadata => metadata.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .OrderBy(metadata => metadata.Iteracion)
                .ToListAsync(cancellationToken);
            List<DAOMetadataEntradaContextoIA> metadataEntradasContextoIA = await contexto.MetadataEntradasContextoIA
                .AsNoTracking()
                .Where(entrada => entrada.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .OrderBy(entrada => entrada.Orden)
                .ToListAsync(cancellationToken);
            List<DAOEjecucionComandoContexto> ejecucionesComandoContexto = await contexto.EjecucionesComandoContexto
                .AsNoTracking()
                .Where(ejecucion => ejecucion.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje)
                .OrderBy(ejecucion => ejecucion.NumeroIntento)
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
                    informacionTecnicaLlamadasIA,
                    metadataEntradasContextoIA,
                    ejecucionesComandoContexto);
            }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarEstadoTimeoutAsync(serviceProvider, idProcesamientoInternoMensaje, logger);
            throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 10 minutos.");
        }

        await RegistrarEstadoTimeoutAsync(serviceProvider, idProcesamientoInternoMensaje, logger);
        throw new TimeoutException("El flujo completo de mensajeria supero el timeout de 10 minutos.");
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
        // Formato: /tmp/per_mensajeria_openrouter_yyyyMMddHHmmss_{Guid}
        string fecha = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string ruta = Path.Combine(Path.GetTempPath(), $"per_mensajeria_openrouter_{fecha}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    private static string CrearPromptAgenteIntegracionOpenRouter()
    {
        return "Esta es una prueba de integracion y debes seguir exactamente tres decisiones. "
            + "Primero solicita la tool comando_pedido_consultar con pedido 54013. "
            + "Solo despues de recibir su resultado role=tool solicita contexto_consultar_mensajes_linea_anterior con ciclosHaciaAtras=1. "
            + "Solo despues de recibir el resultado de la consulta responde sin tools con JSON "
            + "{\"accion\":\"responder\",\"mensajes\":[{\"tipoMensaje\":\"texto\",\"contenido\":\"respuesta\"}]}. "
            + $"La respuesta final debe mencionar el pedido 54013, el estado despachado y la preferencia '{PreferenciaAnterior}' encontrada en el ciclo anterior. "
            + "No repitas tools, no cambies el orden y no respondas antes de completar ambos resultados.";
    }

    private static void AssertRequestContienePromptAgente(string? requestJson, string promptAgente)
    {
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using JsonDocument documento = JsonDocument.Parse(requestJson!);
        JsonElement mensajeSistema = documento.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .First(mensaje => mensaje.GetProperty("role").GetString() == "system");
        string? contenido = mensajeSistema.GetProperty("content").GetString();

        Assert.NotNull(contenido);
        Assert.Contains(promptAgente, contenido);
        Assert.Contains("PROTOCOLO_TECNICO_OBLIGATORIO", contenido);
    }

    private async Task GuardarInformacionTecnicaOpenRouterDisponibleAsync(
        IServiceProvider serviceProvider,
        string directorio,
        long? idProcesamientoInternoMensaje)
    {
        if (idProcesamientoInternoMensaje is null)
        {
            return;
        }

        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            using IServiceScope alcance = serviceProvider.CreateScope();
            MensajeriaContextoDB contexto = alcance.ServiceProvider.GetRequiredService<MensajeriaContextoDB>();
            List<DAOInformacionTecnicaLlamadaIALineaConversacion> metadata = await contexto
                .InformacionTecnicaLlamadasIALineaConversacion
                .AsNoTracking()
                .Where(registro => registro.IDProcesamientoInternoMensaje == idProcesamientoInternoMensaje.Value)
                .OrderBy(registro => registro.Iteracion)
                .ThenBy(registro => registro.ID)
                .ToListAsync(timeout.Token);
            await GuardarInformacionTecnicaOpenRouterAsync(directorio, metadata, timeout.Token);
        }
        catch (Exception excepcion)
        {
            output.WriteLine($"No se pudieron guardar los artefactos OpenRouter del flujo fallido: {excepcion}");
        }
    }

    private static async Task GuardarInformacionTecnicaOpenRouterAsync(
        string directorio,
        IReadOnlyList<DAOInformacionTecnicaLlamadaIALineaConversacion> metadata,
        CancellationToken cancellationToken)
    {
        JsonSerializerOptions opciones = new() { WriteIndented = true };
        string metadataJson = JsonSerializer.Serialize(metadata, opciones);
        await File.WriteAllTextAsync(
            Path.Combine(directorio, "metadata_openrouter.json"),
            metadataJson,
            Encoding.UTF8,
            cancellationToken);

        foreach (DAOInformacionTecnicaLlamadaIALineaConversacion registro in metadata)
        {
            string prefijo = Path.Combine(directorio, $"iteracion_{registro.Iteracion}");
            await File.WriteAllTextAsync(
                $"{prefijo}_request.json",
                FormatearJson(registro.RequestJson),
                Encoding.UTF8,
                cancellationToken);
            await File.WriteAllTextAsync(
                $"{prefijo}_response.json",
                FormatearJson(registro.ResponseJson),
                Encoding.UTF8,
                cancellationToken);
            await File.WriteAllTextAsync(
                $"{prefijo}_content.txt",
                registro.Content ?? string.Empty,
                Encoding.UTF8,
                cancellationToken);
            await File.WriteAllTextAsync(
                $"{prefijo}_reasoning.txt",
                registro.Reasoning ?? string.Empty,
                Encoding.UTF8,
                cancellationToken);
            await File.WriteAllTextAsync(
                $"{prefijo}_reasoning_details.json",
                FormatearJson(registro.ReasoningDetailsJson),
                Encoding.UTF8,
                cancellationToken);
            await File.WriteAllTextAsync(
                $"{prefijo}_metadata.json",
                JsonSerializer.Serialize(registro, opciones),
                Encoding.UTF8,
                cancellationToken);
        }
    }

    private static string FormatearJson(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return contenido ?? string.Empty;
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
        List<DAOInformacionTecnicaLlamadaIALineaConversacion> InformacionTecnicaLlamadasIA,
        List<DAOMetadataEntradaContextoIA> MetadataEntradasContextoIA,
        List<DAOEjecucionComandoContexto> EjecucionesComandoContexto);

    private sealed record CicloAnteriorPrueba(
        long IDLineaConversacion,
        long IDMensaje,
        long IDProcesamientoInternoMensaje);

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

        public List<RegistroComandoEjecutadoPrueba> ComandosEjecutados { get; } = [];

        public List<string> Operaciones { get; } = [];

        public void RegistrarFiltro(string nombre, int iteracion)
        {
            lock (sync)
            {
                Filtros.Add(new RegistroFiltroPrueba(nombre, iteracion));
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

    }

    private sealed record RegistroFiltroPrueba(string Nombre, int Iteracion);

    private sealed record RegistroComandoEjecutadoPrueba(string Pedido, string Estado);

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

    private sealed class ProcesadorResultadoPedidoPrueba : IProcesadorResultadoComando
    {
        public string Tipo => "pedido_prueba";

        public int Version => 1;

        public string Formato => "json";

        public Task<string?> SerializarAsync(object? salida, CancellationToken token = default)
        {
            string? contenido = salida is string salidaTexto
                ? salidaTexto
                : JsonSerializer.Serialize(salida);
            return Task.FromResult<string?>(contenido);
        }

        public Task<object?> DeserializarAsync(string? contenido, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(contenido))
            {
                return Task.FromResult<object?>(null);
            }

            return Task.FromResult<object?>(contenido);
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
