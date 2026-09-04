using BuilderTest.Infraestructura;
using static BuilderTest.Infraestructura.IntegracionCompletaMensajeriaLineaComandoEscenarioPrueba;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
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
using PER.Mensajeria.API.Comunicacion;
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
        RegistroIntegracionMensajeriaPrueba registro = new();
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
        ComunicacionMensajeriaIntegracionPrueba comunicacion = new();
        servicios.AddSingleton(comunicacion);

        servicios.AddLineaComando(
            NombreFactoriaComando,
            RegistrarComandosPruebaAsync);
        LineaComandoBuilder lineaComandoBuilder = servicios.AddLineaComando();

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
            .AgregarWorkerOrquestador()
            .AgregarWorkerMensajeria<ComunicacionMensajeriaIntegracionPrueba>());

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

            DTORegistrarMensajeEntranteSolicitud solicitudEntrada = CrearSolicitudEntrada(
                baseDatos.CuentaCanal);
            string identificadorExternoMensaje = solicitudEntrada.Mensaje.IdentificadorExternoMensaje
                ?? throw new InvalidOperationException("La solicitud de prueba requiere identificador externo.");
            await comunicacion.PublicarEntradaAsync(solicitudEntrada, timeoutFlujo.Token);
            DTORegistrarMensajeEntranteRespuesta respuestaEntrada = await EsperarRegistroEntradaAsync(
                serviceProvider,
                identificadorExternoMensaje,
                timeoutFlujo.Token);
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
            Assert.NotEmpty(resultado.Envios);
            Assert.All(
                resultado.Envios,
                envio => Assert.Equal("enviado", envio.IDEstadoEnvioMensaje));
            Assert.Contains(
                comunicacion.MensajesEnviados,
                mensaje => mensaje.IDEnvioMensaje == resultado.Envios.Single().ID);
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

}
