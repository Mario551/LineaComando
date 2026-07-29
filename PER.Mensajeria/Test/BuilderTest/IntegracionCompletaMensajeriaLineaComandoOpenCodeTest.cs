using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BuilderTest.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Builder;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;
using PER.Mensajeria.Builder;
using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;
using Xunit.Abstractions;
using static BuilderTest.Infraestructura.IntegracionCompletaMensajeriaLineaComandoEscenarioPrueba;

namespace BuilderTest;

public class IntegracionCompletaMensajeriaLineaComandoOpenCodeTest
{
    private static readonly TimeSpan TiempoEsperaFlujo =
        TimeSpan.FromMinutes(10);

    private readonly ITestOutputHelper output;

    public IntegracionCompletaMensajeriaLineaComandoOpenCodeTest(
        ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> Motores
    {
        get
        {
            yield return
            [
                MotorIntegracionCompletaPrueba.PostgreSql
            ];
            yield return
            [
                MotorIntegracionCompletaPrueba.SqlServer
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public async Task FlujoCompleto_BuilderLineaComandoMensajeriaOpenCode_DebeRegistrarSalida(
        MotorIntegracionCompletaPrueba motor)
    {
        RegistroArtefactosOpenCodePrueba artefactos = new();
        output.WriteLine($"Archivos OpenCode: {artefactos.Directorio}");

        try
        {
            string servidorTexto = LeerVariableObligatoria(
                "OPENCODE_SERVER_LOCAL",
                "La variable OPENCODE_SERVER_LOCAL es obligatoria para la integracion real con OpenCode.");
            string usuario = LeerVariableObligatoria(
                "OPENCODE_SERVER_USERNAME",
                "La variable OPENCODE_SERVER_USERNAME es obligatoria para la autenticacion Basic de OpenCode.");
            string contrasena = LeerVariableObligatoria(
                "OPENCODE_SERVER_PASSWORD",
                "La variable OPENCODE_SERVER_PASSWORD es obligatoria para la autenticacion Basic de OpenCode.");
            string nombreAgente = LeerVariableObligatoria(
                "OPENCODE_SERVER_LOCAL_NOMBRE_AGENTE_TEST",
                "La variable OPENCODE_SERVER_LOCAL_NOMBRE_AGENTE_TEST es obligatoria para seleccionar el agente OpenCode.");
            Uri servidor = CrearServidorOpenCode(servidorTexto);

            await artefactos.GuardarEjecucionAsync(new
            {
                motor = motor.ToString(),
                servidor = SanitizarServidor(servidor),
                nombreAgente,
                fechaInicio = DateTime.UtcNow,
                idEjecucion = Guid.NewGuid()
            });
            await ComprobarServidorOpenCodeAsync(
                servidor,
                usuario,
                contrasena,
                artefactos);

            await EjecutarFlujoCompletoAsync(
                motor,
                servidor,
                usuario,
                contrasena,
                nombreAgente,
                artefactos);
        }
        catch (Exception excepcion)
        {
            await artefactos.GuardarErrorAsync(excepcion);
            throw;
        }
        finally
        {
            await artefactos.GuardarManifestAsync();
        }
    }

    private async Task EjecutarFlujoCompletoAsync(
        MotorIntegracionCompletaPrueba motor,
        Uri servidor,
        string usuario,
        string contrasena,
        string nombreAgente,
        RegistroArtefactosOpenCodePrueba artefactos)
    {
        string promptAgente = CrearPromptAgenteIntegracionOpenCode();
        ConfiguracionBaseDatosPrueba baseDatos =
            CrearConfiguracionBaseDatos(motor);
        RegistroIntegracionMensajeriaPrueba registro = new();
        RegistroLoggerPrueba registroLogger = new(output);
        ServiceCollection servicios = new();
        servicios.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new LoggerProviderPrueba(registroLogger));
        });
        servicios.AddSingleton(registro);
        servicios.AddSingleton(artefactos);
        ComunicacionMensajeriaIntegracionPrueba comunicacion = new();
        servicios.AddSingleton(comunicacion);

        LineaComandoBuilder lineaComandoBuilder = servicios.AddLineaComando(
            async (
                serviceProvider,
                builderInicializador,
                cancellationToken) =>
            {
                await RegistrarComandosPruebaAsync(
                    serviceProvider,
                    builderInicializador,
                    cancellationToken);
            });

        ConfigurarBaseDatos(lineaComandoBuilder, baseDatos);

        lineaComandoBuilder.AgregarMensajeria(builder => builder
            .ConfigurarLineaConversacion(TimeSpan.FromHours(24))
            .ConfigurarContextoConversacion(
                new ConfiguracionContextoConversacion
                {
                    MaximoIteraciones = 4
                })
            .ConfigurarContexto(contexto => contexto
                .AgregarFiltro<PrimerFiltroContextoPrueba>()
                .AgregarFiltro<SegundoFiltroContextoPrueba>()
                .UsarCatalogoComandos<CatalogoComandosLineaComandoPrueba>()
                .UsarIntencionOpenCode(
                    promptAgente,
                    nombreAgente,
                    configuracion =>
                    {
                        configuracion.Servidor = servidor;
                        configuracion.AutenticacionBasica =
                            new ConfiguracionAutenticacionBasicaOpenCode(
                                usuario,
                                contrasena);
                        configuracion.Timeout = TimeSpan.FromMinutes(5);
                    })
                .UsarIntencion<IntencionOpenCodeIntegracionPrueba>()
                .UsarEjecutorLineaComando())
            .AgregarWorkerOrquestador()
            .AgregarWorkerMensajeria<
                ComunicacionMensajeriaIntegracionPrueba>());

        ReconfigurarMensajeriaContextoDBParaEsquemaPrueba(
            servicios,
            baseDatos);
        lineaComandoBuilder.Build();

        await using ServiceProvider serviceProvider =
            servicios.BuildServiceProvider();
        await serviceProvider.InicializarLineaComandoAsync();
        await CrearCuentaCanalAsync(
            serviceProvider,
            baseDatos.CuentaCanal);
        CicloAnteriorPrueba cicloAnterior =
            await CrearCicloAnteriorAsync(
                serviceProvider,
                baseDatos.CuentaCanal);

        List<IHostedService> hostedServices =
            serviceProvider.GetServices<IHostedService>().ToList();
        using CancellationTokenSource timeoutFlujo =
            new(TiempoEsperaFlujo);
        long? idProcesamientoInternoMensaje = null;

        try
        {
            await IniciarHostedServicesAsync(
                hostedServices,
                timeoutFlujo.Token);

            DTORegistrarMensajeEntranteSolicitud solicitudEntrada =
                CrearSolicitudEntrada(baseDatos.CuentaCanal);
            string identificadorExternoMensaje =
                solicitudEntrada.Mensaje.IdentificadorExternoMensaje
                ?? throw new InvalidOperationException(
                    "La solicitud de prueba requiere identificador externo.");
            await comunicacion.PublicarEntradaAsync(
                solicitudEntrada,
                timeoutFlujo.Token);
            DTORegistrarMensajeEntranteRespuesta respuestaEntrada =
                await EsperarRegistroEntradaAsync(
                    serviceProvider,
                    identificadorExternoMensaje,
                    timeoutFlujo.Token);
            idProcesamientoInternoMensaje =
                respuestaEntrada.IDProcesamientoInternoMensaje;

            ILogger<IntegracionCompletaMensajeriaLineaComandoOpenCodeTest>
                logger = serviceProvider.GetRequiredService<
                    ILogger<
                        IntegracionCompletaMensajeriaLineaComandoOpenCodeTest>>();
            ResultadoFlujoCompletoPrueba resultado =
                await EsperarProcesamientoAsync(
                    serviceProvider,
                    respuestaEntrada.IDProcesamientoInternoMensaje,
                    logger,
                    timeoutFlujo.Token);

            await GuardarEstadoFlujoAsync(
                artefactos,
                resultado);
            ValidarFlujoComun(
                respuestaEntrada,
                cicloAnterior,
                resultado,
                comunicacion,
                registro);
            ValidarOpenCode(
                resultado,
                artefactos,
                nombreAgente,
                usuario,
                contrasena);
            registroLogger.AssertSinErrores();
        }
        catch (OperationCanceledException)
            when (timeoutFlujo.IsCancellationRequested)
        {
            await GuardarEstadoDisponibleAsync(
                serviceProvider,
                artefactos,
                idProcesamientoInternoMensaje);
            throw new TimeoutException(
                "El flujo completo de mensajeria OpenCode supero el timeout de 10 minutos.");
        }
        catch
        {
            await GuardarEstadoDisponibleAsync(
                serviceProvider,
                artefactos,
                idProcesamientoInternoMensaje);
            throw;
        }
        finally
        {
            using CancellationTokenSource timeoutApagado =
                new(TimeSpan.FromSeconds(10));
            await DetenerHostedServicesAsync(
                hostedServices,
                timeoutApagado.Token);
        }
    }

    private static async Task ComprobarServidorOpenCodeAsync(
        Uri servidor,
        string usuario,
        string contrasena,
        RegistroArtefactosOpenCodePrueba artefactos)
    {
        using HttpClient cliente = new()
        {
            BaseAddress = NormalizarServidor(servidor),
            Timeout = TimeSpan.FromSeconds(10)
        };
        string credenciales = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{usuario}:{contrasena}"));
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                credenciales);

        try
        {
            using HttpResponseMessage respuesta =
                await cliente.GetAsync(
                    new Uri(servidor, "/global/health"));
            string cuerpo = await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
            {
                string error =
                    $"OpenCode no esta disponible. GET /global/health devolvio HTTP {(int)respuesta.StatusCode}.";
                await artefactos.GuardarPreflightFallidoAsync(
                    error,
                    respuesta.StatusCode,
                    cuerpo);
                throw new InvalidOperationException(error);
            }

            bool saludable = false;
            try
            {
                using JsonDocument documento = JsonDocument.Parse(cuerpo);
                saludable =
                    documento.RootElement.TryGetProperty(
                        "healthy",
                        out JsonElement healthy)
                    && healthy.ValueKind == JsonValueKind.True;
            }
            catch (JsonException excepcion)
            {
                string error =
                    "OpenCode devolvio JSON invalido en GET /global/health.";
                await artefactos.GuardarPreflightFallidoAsync(
                    error,
                    respuesta.StatusCode,
                    cuerpo);
                throw new InvalidOperationException(
                    error,
                    excepcion);
            }

            if (!saludable)
            {
                const string error =
                    "OpenCode respondio GET /global/health, pero healthy no es true.";
                await artefactos.GuardarPreflightFallidoAsync(
                    error,
                    respuesta.StatusCode,
                    cuerpo);
                throw new InvalidOperationException(error);
            }

            await artefactos.GuardarPreflightExitosoAsync(
                respuesta.StatusCode,
                cuerpo);
        }
        catch (Exception excepcion)
            when (excepcion is HttpRequestException
                or TaskCanceledException)
        {
            string error =
                $"No fue posible conectar con OpenCode en {SanitizarServidor(servidor)}: {excepcion.Message}";
            await artefactos.GuardarPreflightFallidoAsync(error);
            throw new InvalidOperationException(
                error,
                excepcion);
        }
    }

    private static void ValidarFlujoComun(
        DTORegistrarMensajeEntranteRespuesta respuestaEntrada,
        CicloAnteriorPrueba cicloAnterior,
        ResultadoFlujoCompletoPrueba resultado,
        ComunicacionMensajeriaIntegracionPrueba comunicacion,
        RegistroIntegracionMensajeriaPrueba registro)
    {
        Assert.True(respuestaEntrada.Registrado);
        Assert.Equal(
            "procesado",
            resultado.Procesamiento.IDEstadoProcesamientoInternoMensaje);
        Assert.NotNull(resultado.Procesamiento.FechaProcesado);
        Assert.Null(resultado.Procesamiento.Error);
        Assert.Equal(2, resultado.MensajesEntrada.Count);
        Assert.Contains(
            resultado.MensajesEntrada,
            mensaje => mensaje.ID == cicloAnterior.IDMensaje);
        Assert.Contains(
            resultado.MensajesEntrada,
            mensaje => mensaje.ID != cicloAnterior.IDMensaje
                && mensaje.Contenido?.Contains(Pedido) == true);
        Assert.NotEmpty(resultado.MensajesSalida);
        Assert.NotEmpty(resultado.Envios);
        Assert.All(
            resultado.Envios,
            envio => Assert.Equal(
                "enviado",
                envio.IDEstadoEnvioMensaje));
        Assert.Contains(
            comunicacion.MensajesEnviados,
            mensaje => mensaje.IDEnvioMensaje
                == resultado.Envios.Single().ID);
        Assert.Equal(
            [
                nameof(AccionContextoTipo.Comando),
                nameof(
                    AccionContextoTipo
                        .ConsultarMensajesLineaAnterior),
                nameof(AccionContextoTipo.Responder)
            ],
            resultado.InformacionTecnicaLlamadasIA
                .OrderBy(metadata => metadata.Iteracion)
                .Select(metadata => metadata.AccionDecidida));
        Assert.Equal(
            [
                ("user", "mensaje_entrada"),
                ("assistant", "decision_comando"),
                ("tool", "resultado_comando"),
                (
                    "assistant",
                    "decision_consulta_mensajes_linea_anterior"),
                (
                    "tool",
                    "resultado_consulta_mensajes_linea_anterior"),
                ("assistant", "respuesta_final")
            ],
            resultado.MetadataEntradasContextoIA
                .OrderBy(entrada => entrada.Orden)
                .Select(entrada => (
                    entrada.IDRolContextoIA,
                    entrada.IDTipoEntradaContextoIA)));
        Assert.Equal(
            [1, 2, 3, 4, 5, 6],
            resultado.MetadataEntradasContextoIA
                .OrderBy(entrada => entrada.Orden)
                .Select(entrada => entrada.Orden));
        Assert.All(
            resultado.MetadataEntradasContextoIA.Where(
                entrada => entrada.IDRolContextoIA == "assistant"),
            entrada => Assert.NotNull(
                entrada.IDInformacionTecnicaLlamadaIA));
        Assert.All(
            resultado.MetadataEntradasContextoIA,
            entrada => Assert.NotEqual(
                default,
                entrada.FechaEntrada));

        DAOMetadataEntradaContextoIA decisionComando =
            resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA
                    == "decision_comando");
        DAOMetadataEntradaContextoIA resultadoComando =
            resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA
                    == "resultado_comando");
        DAOMetadataEntradaContextoIA decisionConsulta =
            resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA
                    == "decision_consulta_mensajes_linea_anterior");
        DAOMetadataEntradaContextoIA resultadoConsulta =
            resultado.MetadataEntradasContextoIA.Single(
                entrada => entrada.IDTipoEntradaContextoIA
                    == "resultado_consulta_mensajes_linea_anterior");
        Assert.False(
            string.IsNullOrWhiteSpace(decisionComando.ToolCallID));
        Assert.Equal(
            decisionComando.ToolCallID,
            resultadoComando.ToolCallID);
        Assert.False(
            string.IsNullOrWhiteSpace(decisionConsulta.ToolCallID));
        Assert.Equal(
            decisionConsulta.ToolCallID,
            resultadoConsulta.ToolCallID);

        using (JsonDocument referenciaConsulta =
            JsonDocument.Parse(resultadoConsulta.Contenido!))
        {
            Assert.Equal(
                "cargada",
                referenciaConsulta.RootElement
                    .GetProperty("estado")
                    .GetString());
            Assert.Equal(
                cicloAnterior.IDLineaConversacion,
                referenciaConsulta.RootElement
                    .GetProperty("idLineaConversacion")
                    .GetInt64());
            Assert.Equal(
                cicloAnterior.IDProcesamientoInternoMensaje,
                referenciaConsulta.RootElement
                    .GetProperty("idProcesamientoInternoMensaje")
                    .GetInt64());
            Assert.Equal(
                2,
                referenciaConsulta.RootElement
                    .GetProperty("cantidadEntradas")
                    .GetInt32());
        }

        DAOMensaje mensajeSalida = Assert.Single(
            resultado.MensajesSalida);
        Assert.Contains(
            Pedido,
            mensajeSalida.Contenido ?? string.Empty);
        Assert.Contains(
            EstadoPedido,
            mensajeSalida.Contenido ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            PreferenciaAnterior,
            mensajeSalida.Contenido ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        DAOEjecucionComandoContexto ejecucionComando =
            Assert.Single(resultado.EjecucionesComandoContexto);
        Assert.Equal(
            CodigoComando,
            ejecucionComando.CodigoComando);
        Assert.Equal(
            "completada",
            ejecucionComando.IDEstadoEjecucionComandoContexto);
        Assert.False(ejecucionComando.Activa);
        Assert.False(
            string.IsNullOrWhiteSpace(
                ejecucionComando.IdentificadorExterno));
        Assert.Contains(
            Pedido,
            ejecucionComando.ParametrosJson);
        Assert.Contains(
            registro.ComandosEjecutados,
            comando => comando.Pedido == Pedido
                && comando.Estado == EstadoPedido);
        Assert.True(
            registro.Operaciones.IndexOf("comando_ejecutado") >= 0);

        List<RegistroFiltroPrueba> filtrosPrimeraIteracion =
            registro.Filtros
                .Where(filtro => filtro.Iteracion == 1)
                .ToList();
        Assert.Equal(
            ["primer_filtro", "segundo_filtro"],
            filtrosPrimeraIteracion
                .Select(filtro => filtro.Nombre)
                .ToList());
        Assert.True(
            registro.Filtros
                .Select(filtro => filtro.Iteracion)
                .Distinct()
                .Count() >= 3);
    }

    private static void ValidarOpenCode(
        ResultadoFlujoCompletoPrueba resultado,
        RegistroArtefactosOpenCodePrueba artefactos,
        string nombreAgente,
        string usuario,
        string contrasena)
    {
        Assert.Equal(
            3,
            resultado.InformacionTecnicaLlamadasIA.Count);
        Assert.All(
            resultado.InformacionTecnicaLlamadasIA,
            informacion =>
            {
                Assert.Equal(
                    nameof(OpenCodeAgenteAdaptador),
                    informacion.Adaptador);
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        informacion.Proveedor));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        informacion.Modelo));
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        informacion.ResponseJson));
                AssertRequestUsaAgente(
                    informacion.RequestJson,
                    nombreAgente);
            });

        IReadOnlyList<LlamadaOpenCodePrueba> llamadas =
            artefactos.Llamadas;
        Assert.Equal(
            3,
            llamadas.Count(llamada =>
                llamada.Operacion == "crear_sesion"));
        Assert.Equal(
            3,
            llamadas.Count(llamada =>
                llamada.Operacion == "enviar_mensaje"));
        Assert.Equal(
            3,
            llamadas.Count(llamada =>
                llamada.Operacion == "eliminar_sesion"));
        Assert.DoesNotContain(
            llamadas,
            llamada => llamada.Operacion == "abortar_sesion");
        Assert.All(
            llamadas,
            llamada => Assert.True(
                llamada.Exitoso,
                $"La llamada {llamada.Secuencia} ({llamada.Operacion}) fallo: {llamada.Error}"));

        foreach (int iteracion in new[] { 1, 2, 3 })
        {
            LlamadaOpenCodePrueba creacion = Assert.Single(
                llamadas,
                llamada =>
                    llamada.Iteracion == iteracion
                    && llamada.Operacion == "crear_sesion");
            LlamadaOpenCodePrueba mensaje = Assert.Single(
                llamadas,
                llamada =>
                    llamada.Iteracion == iteracion
                    && llamada.Operacion == "enviar_mensaje");
            LlamadaOpenCodePrueba eliminacion = Assert.Single(
                llamadas,
                llamada =>
                    llamada.Iteracion == iteracion
                    && llamada.Operacion == "eliminar_sesion");
            Assert.False(
                string.IsNullOrWhiteSpace(creacion.IDSesion));
            Assert.Equal(creacion.IDSesion, mensaje.IDSesion);
            Assert.Equal(creacion.IDSesion, eliminacion.IDSesion);
        }

        string credencialesBase64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{usuario}:{contrasena}"));
        foreach (string archivo in Directory.EnumerateFiles(
            artefactos.Directorio))
        {
            string contenido = File.ReadAllText(archivo);
            Assert.DoesNotContain(
                contrasena,
                contenido,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                credencialesBase64,
                contenido,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Authorization",
                contenido,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertRequestUsaAgente(
        string? requestJson,
        string nombreAgente)
    {
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using JsonDocument documento = JsonDocument.Parse(requestJson!);
        Assert.Equal(
            nombreAgente,
            documento.RootElement
                .GetProperty("agent")
                .GetString());
        Assert.False(
            documento.RootElement.TryGetProperty(
                "model",
                out _));
        Assert.False(
            documento.RootElement.TryGetProperty(
                "directory",
                out _));
        JsonElement herramientas =
            documento.RootElement.GetProperty("tools");
        Assert.Equal(
            JsonValueKind.Object,
            herramientas.ValueKind);
        Assert.All(
            herramientas.EnumerateObject(),
            herramienta => Assert.False(
                herramienta.Value.GetBoolean()));
    }

    private static async Task GuardarEstadoFlujoAsync(
        RegistroArtefactosOpenCodePrueba artefactos,
        ResultadoFlujoCompletoPrueba resultado)
    {
        await artefactos.GuardarJsonAsync(
            "metadata_bd.json",
            resultado.InformacionTecnicaLlamadasIA);
        await artefactos.GuardarJsonAsync(
            "metadata_entradas_contexto_ia.json",
            resultado.MetadataEntradasContextoIA);
        await artefactos.GuardarJsonAsync(
            "estado_final.json",
            new
            {
                resultado.Procesamiento,
                resultado.MensajesEntrada,
                resultado.MensajesSalida,
                resultado.Envios,
                resultado.EjecucionesComandoContexto
            });
    }

    private static async Task GuardarEstadoDisponibleAsync(
        IServiceProvider serviceProvider,
        RegistroArtefactosOpenCodePrueba artefactos,
        long? idProcesamientoInternoMensaje)
    {
        if (idProcesamientoInternoMensaje is null)
        {
            return;
        }

        try
        {
            using CancellationTokenSource timeout =
                new(TimeSpan.FromSeconds(10));
            using IServiceScope alcance =
                serviceProvider.CreateScope();
            MensajeriaContextoDB contexto =
                alcance.ServiceProvider.GetRequiredService<
                    MensajeriaContextoDB>();
            DAOProcesamientoInternoMensaje? procesamiento =
                await contexto.ProcesamientosInternosMensaje
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        actual => actual.ID
                            == idProcesamientoInternoMensaje.Value,
                        timeout.Token);
            List<DAOInformacionTecnicaLlamadaIALineaConversacion>
                informacionTecnica = await contexto
                    .InformacionTecnicaLlamadasIALineaConversacion
                    .AsNoTracking()
                    .Where(actual =>
                        actual.IDProcesamientoInternoMensaje
                            == idProcesamientoInternoMensaje.Value)
                    .OrderBy(actual => actual.Iteracion)
                    .ThenBy(actual => actual.ID)
                    .ToListAsync(timeout.Token);
            List<DAOMetadataEntradaContextoIA> entradas =
                await contexto.MetadataEntradasContextoIA
                    .AsNoTracking()
                    .Where(actual =>
                        actual.IDProcesamientoInternoMensaje
                            == idProcesamientoInternoMensaje.Value)
                    .OrderBy(actual => actual.Orden)
                    .ThenBy(actual => actual.ID)
                    .ToListAsync(timeout.Token);

            await artefactos.GuardarJsonAsync(
                "metadata_bd.json",
                informacionTecnica,
                timeout.Token);
            await artefactos.GuardarJsonAsync(
                "metadata_entradas_contexto_ia.json",
                entradas,
                timeout.Token);
            await artefactos.GuardarJsonAsync(
                "estado_final.json",
                new
                {
                    procesamiento
                },
                timeout.Token);
        }
        catch (Exception excepcion)
        {
            await artefactos.GuardarErrorAsync(excepcion);
        }
    }

    private static string CrearPromptAgenteIntegracionOpenCode()
    {
        return "Esta es una prueba de integracion y debes seguir exactamente tres decisiones. "
            + "Primero responde con accion comando, codigoComando 'pedido consultar' y parametro pedido '54013'. "
            + "Solo despues de recibir la metadata-entrada tool resultado_comando solicita accion consultar_mensajes_linea_anterior con ciclosHaciaAtras 1. "
            + "Solo despues de recibir la metadata-entrada tool resultado_consulta_mensajes_linea_anterior responde con accion responder. "
            + $"La respuesta final debe mencionar el pedido {Pedido}, el estado {EstadoPedido} y la preferencia '{PreferenciaAnterior}' encontrada en el ciclo anterior. "
            + "No repitas acciones, no cambies el orden y no respondas antes de recibir ambos resultados.";
    }

    private static Uri CrearServidorOpenCode(string servidor)
    {
        if (!Uri.TryCreate(
                servidor,
                UriKind.Absolute,
                out Uri? uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "OPENCODE_SERVER_LOCAL debe ser una URI HTTP o HTTPS absoluta.");
        }

        return uri;
    }

    private static Uri NormalizarServidor(Uri servidor)
    {
        string valor = servidor.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? servidor.AbsoluteUri
            : servidor.AbsoluteUri + "/";
        return new Uri(valor);
    }

    private static string SanitizarServidor(Uri servidor)
    {
        UriBuilder builder = new(servidor)
        {
            UserName = string.Empty,
            Password = string.Empty
        };
        return builder.Uri.AbsoluteUri;
    }
}
