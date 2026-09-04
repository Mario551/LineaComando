using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PER.Comandos.LineaComandos;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using Xunit.Abstractions;

namespace AplicacionTest;

public class IntencionOpenRouterLoopToolTest
{
    private const string ModeloOpenRoute = "moonshotai/kimi-k2.6";
    private const string Pedido = "54013";
    private const string ClienteId = "cliente-7701";
    private const string EnvioId = "envio-9902";
    private const string Guia = "GUIA-ABC-54013";
    private const string EstadoPedido = "despachado";
    private const string TituloRazonamientoReportado = "RAZONAMIENTO_REPORTADO_OPENROUTER";
    private const string PreguntaInicial = "Ejecuta el flujo de prueba para el pedido 54013. Primero consulta el pedido, con el clienteId consulta el cliente, con el envioId consulta el envio y luego responde el resumen final.";
    private const int MaximoIteraciones = 6;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly ITestOutputHelper output;

    public IntencionOpenRouterLoopToolTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task LoopTool_OpenRouter_DebeEjecutarTresComandosConFactoriaYAcumularResultados()
    {
        string apiKey = LeerVariableObligatoria(
            "OPENROUTE_MENSAJERIA",
            "La variable de entorno OPENROUTE_MENSAJERIA es obligatoria para probar el loop tool real con OpenRouter.");
        string directorio = CrearDirectorioOpenRouterPrueba();
        output.WriteLine($"Archivos OpenRouter loop tool: {directorio}");

        RegistroLoopToolPrueba registro = new();
        IFactoriaAbstractaComandos<string, ResultadoComando> factoria = CrearFactoria(registro);
        IReadOnlyList<DTOOpenRouterToolPrueba> tools = CrearTools();
        List<DTOOpenRouterMensajePrueba> mensajes = CrearMensajesIniciales();
        List<TrazaLoopToolPrueba> traza = [];

        using HttpClient cliente = CrearCliente(apiKey);
        string? respuestaFinal = null;

        for (int iteracion = 1; iteracion <= MaximoIteraciones; iteracion++)
        {
            DTOOpenRouterChatSolicitudPrueba solicitud = new()
            {
                Model = ModeloOpenRoute,
                Temperature = 0,
                MaxCompletionTokens = 10000,
                ToolChoice = "auto",
                Tools = tools,
                Messages = mensajes
            };

            registro.RegistrarSnapshot(
                iteracion,
                mensajes.Count(mensaje => mensaje.Role == "tool"),
                mensajes.Count(EsMensajeRazonamientoReportado),
                mensajes.Count(EsMensajeInstruccionContinuidad));
            DateTime fechaSolicitudUtc = DateTime.UtcNow;
            ResultadoOpenRouterPrueba resultadoOpenRouter = await EnviarAsync(cliente, solicitud, iteracion, directorio, CancellationToken.None);
            DateTime fechaRespuestaUtc = DateTime.UtcNow;
            DTOOpenRouterChoicePrueba choice = resultadoOpenRouter.Respuesta.Choices[0];
            DTOOpenRouterMensajePrueba mensajeModelo = choice.Message;

            if (mensajeModelo.ToolCalls.Count > 0)
            {
                if (mensajeModelo.ToolCalls.Count != 1)
                {
                    throw new InvalidOperationException($"OpenRouter pidio {mensajeModelo.ToolCalls.Count} herramientas en la iteracion {iteracion}. La prueba exige una herramienta por iteracion para validar secuencia.");
                }

                if (registro.ComandosEjecutados.Count >= 3)
                {
                    throw new InvalidOperationException("OpenRouter pidio herramientas despues de ejecutar los tres comandos esperados.");
                }

                mensajes.Add(DTOOpenRouterMensajePrueba.AssistantToolCalls(mensajeModelo.ToolCalls));

                DTOOpenRouterToolCallPrueba toolCall = mensajeModelo.ToolCalls[0];
                ResultadoToolLoopPrueba resultadoTool = await EjecutarToolAsync(factoria, toolCall, registro, CancellationToken.None);
                mensajes.Add(DTOOpenRouterMensajePrueba.Tool(toolCall.Id, resultadoTool.ContenidoJson));
                await GuardarArchivoAsync(directorio, iteracion, "tool_result.json", FormatearJson(resultadoTool.ContenidoJson), CancellationToken.None);

                TrazaLoopToolPrueba entradaTraza = CrearEntradaTraza(
                    iteracion,
                    fechaSolicitudUtc,
                    fechaRespuestaUtc,
                    choice,
                    toolCall,
                    resultadoTool,
                    resultadoOpenRouter.CuerpoRespuestaJson);
                traza.Add(entradaTraza);
                string mensajeRazonamiento = CrearMensajeRazonamientoReportado(entradaTraza);
                string mensajeContinuidad = CrearMensajeContinuidad(entradaTraza);
                mensajes.Add(DTOOpenRouterMensajePrueba.Assistant(mensajeRazonamiento));
                mensajes.Add(DTOOpenRouterMensajePrueba.User(mensajeContinuidad));
                await GuardarTrazaAsync(directorio, iteracion, entradaTraza, traza, mensajeContinuidad, null, CancellationToken.None);
                continue;
            }

            if (registro.ComandosEjecutados.Count < 3)
            {
                throw new InvalidOperationException($"OpenRouter respondio antes de ejecutar los tres comandos. Ejecutados={string.Join(", ", registro.ComandosEjecutados)}. Respuesta={mensajeModelo.Content}");
            }

            respuestaFinal = mensajeModelo.Content;
            await GuardarArchivoAsync(directorio, iteracion, "final_content.txt", respuestaFinal ?? string.Empty, CancellationToken.None);
            await GuardarTrazaAcumuladaAsync(directorio, traza, respuestaFinal, CancellationToken.None);
            break;
        }

        if (string.IsNullOrWhiteSpace(respuestaFinal))
        {
            throw new TimeoutException($"OpenRouter no devolvio respuesta final despues de {MaximoIteraciones} iteraciones. Revisar archivos en {directorio}.");
        }

        Assert.Equal(["pedido consultar", "cliente consultar", "envio consultar"], registro.ComandosEjecutados);
        Assert.Equal(["pedido consultar", "cliente consultar", "envio consultar"], traza.Select(entrada => entrada.CodigoComando).ToList());
        Assert.Contains(registro.SnapshotsSolicitudes, snapshot => snapshot.Iteracion > 1 && snapshot.CantidadMensajesTool > 0);
        Assert.Contains(registro.SnapshotsSolicitudes, snapshot => snapshot.Iteracion == 2 && snapshot.CantidadMensajesRazonamiento == 1 && snapshot.CantidadMensajesContinuidad == 1);
        Assert.Contains(registro.SnapshotsSolicitudes, snapshot => snapshot.Iteracion == 3 && snapshot.CantidadMensajesRazonamiento == 2 && snapshot.CantidadMensajesContinuidad == 2);
        Assert.Contains(registro.SnapshotsSolicitudes, snapshot => snapshot.Iteracion == 4 && snapshot.CantidadMensajesRazonamiento == 3 && snapshot.CantidadMensajesContinuidad == 3);
        Assert.Equal(3, registro.SnapshotsSolicitudes.Max(snapshot => snapshot.CantidadMensajesTool));
        Assert.Equal(3, traza.Count);
        Assert.All(traza, entrada => Assert.False(string.IsNullOrWhiteSpace(entrada.RazonamientoReportado)));
        Assert.All(traza, entrada => Assert.False(string.IsNullOrWhiteSpace(entrada.ToolSolicitada)));
        Assert.All(traza, entrada => Assert.False(string.IsNullOrWhiteSpace(entrada.ResultadoComandoJson)));
        Assert.Contains(Pedido, respuestaFinal);
        Assert.Contains(EstadoPedido, respuestaFinal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clienteId", respuestaFinal, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            respuestaFinal.Contains("envioId", StringComparison.OrdinalIgnoreCase) || respuestaFinal.Contains(Guia, StringComparison.OrdinalIgnoreCase),
            "La respuesta final debe contener envioId o la guia generada por el tercer comando.");
    }

    private static HttpClient CrearCliente(string apiKey)
    {
        HttpClient cliente = new()
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return cliente;
    }

    private static IFactoriaAbstractaComandos<string, ResultadoComando> CrearFactoria(
        RegistroLoopToolPrueba registro)
    {
        FactoriaComandos<string, ResultadoComando> pedidos = new("pedido");
        pedidos.Add(
            "consultar",
            CrearNodo(parametros => new ConsultarPedidoComando(registro, parametros)));
        FactoriaComandos<string, ResultadoComando> clientes = new("cliente");
        clientes.Add(
            "consultar",
            CrearNodo(parametros => new ConsultarClienteComando(registro, parametros)));
        FactoriaComandos<string, ResultadoComando> envios = new("envio");
        envios.Add(
            "consultar",
            CrearNodo(parametros => new ConsultarEnvioComando(registro, parametros)));
        return new FactoriaAbstractaComandos<string, ResultadoComando>([pedidos, clientes, envios]);
    }

    private static Nodo<string, ResultadoComando> CrearNodo(Func<ICollection<Parametro>, ComandoBase<string, ResultadoComando>> crearComando)
    {
        return new Nodo<string, ResultadoComando>(parametros => crearComando(parametros));
    }

    private static List<DTOOpenRouterMensajePrueba> CrearMensajesIniciales()
    {
        return
        [
            new DTOOpenRouterMensajePrueba
            {
                Role = "system",
                Content = "Eres un motor de prueba de tool-calling para mensajeria. Debes usar herramientas reales antes de responder. Usa exactamente este orden: primero pedido_consultar, luego cliente_consultar, luego envio_consultar. No respondas al usuario hasta completar las tres herramientas. La respuesta final debe mencionar literalmente pedido 54013, estado despachado, clienteId y envioId o la guia."
            },
            new DTOOpenRouterMensajePrueba
            {
                Role = "user",
                Content = PreguntaInicial
            }
        ];
    }

    private static IReadOnlyList<DTOOpenRouterToolPrueba> CrearTools()
    {
        return
        [
            CrearTool(
                "pedido_consultar",
                "Ejecuta el comando LineaComando 'pedido consultar'. Devuelve clienteId, pedido y estado.",
                "pedido",
                "Numero de pedido a consultar."),
            CrearTool(
                "cliente_consultar",
                "Ejecuta el comando LineaComando 'cliente consultar'. Devuelve envioId, cliente y segmento.",
                "clienteId",
                "Identificador de cliente retornado por pedido_consultar."),
            CrearTool(
                "envio_consultar",
                "Ejecuta el comando LineaComando 'envio consultar'. Devuelve guia, transportadora y estadoEnvio.",
                "envioId",
                "Identificador de envio retornado por cliente_consultar.")
        ];
    }

    private static DTOOpenRouterToolPrueba CrearTool(string nombre, string descripcion, string parametro, string descripcionParametro)
    {
        return new DTOOpenRouterToolPrueba
        {
            Function = new DTOOpenRouterFuncionPrueba
            {
                Name = nombre,
                Description = descripcion,
                Parameters = new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new Dictionary<string, object?>
                    {
                        [parametro] = new Dictionary<string, object?>
                        {
                            ["type"] = "string",
                            ["description"] = descripcionParametro
                        }
                    },
                    ["required"] = new[] { parametro }
                }
            }
        };
    }

    private static async Task<ResultadoOpenRouterPrueba> EnviarAsync(
        HttpClient cliente,
        DTOOpenRouterChatSolicitudPrueba solicitud,
        int iteracion,
        string directorio,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(solicitud, OpcionesJson);
        await GuardarArchivoAsync(directorio, iteracion, "request.json", FormatearJson(json), cancellationToken);

        using StringContent contenido = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage respuesta = await cliente.PostAsync(
            "https://openrouter.ai/api/v1/chat/completions",
            contenido,
            cancellationToken);
        string cuerpoRespuesta = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        await GuardarArchivoAsync(directorio, iteracion, "response.json", FormatearJson(cuerpoRespuesta), cancellationToken);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenRouter devolvio {(int)respuesta.StatusCode}. Revisar {Path.Combine(directorio, $"iteracion_{iteracion}_response.json")}");
        }

        DTOOpenRouterChatRespuestaPrueba? dtoRespuesta = JsonSerializer.Deserialize<DTOOpenRouterChatRespuestaPrueba>(cuerpoRespuesta, OpcionesJson);
        if (dtoRespuesta is null || dtoRespuesta.Choices.Count == 0)
        {
            throw new InvalidOperationException($"OpenRouter no devolvio choices. Revisar {Path.Combine(directorio, $"iteracion_{iteracion}_response.json")}");
        }

        return new ResultadoOpenRouterPrueba(dtoRespuesta, cuerpoRespuesta);
    }

    private static async Task<ResultadoToolLoopPrueba> EjecutarToolAsync(
        IFactoriaAbstractaComandos<string, ResultadoComando> factoria,
        DTOOpenRouterToolCallPrueba toolCall,
        RegistroLoopToolPrueba registro,
        CancellationToken cancellationToken)
    {
        string codigoComando = ObtenerCodigoComando(toolCall.Function.Name);
        JsonElement argumentos = LeerArgumentos(toolCall);
        LineaComando lineaComando = CrearLineaComando(toolCall.Function.Name, argumentos);
        IComando<string, ResultadoComando> comando = factoria.Crear(lineaComando);
        ResultadoComando resultado = await comando.EjecutarAsync(toolCall.Function.Arguments, cancellationToken);

        if (!resultado.Exitoso)
        {
            throw new InvalidOperationException($"El comando {codigoComando} fallo: {resultado.MensajeError}");
        }

        registro.RegistrarComando(codigoComando);
        Dictionary<string, object?> contenido = new()
        {
            ["codigoComando"] = codigoComando,
            ["exitoso"] = true,
            ["salida"] = resultado.Salida
        };
        string contenidoJson = JsonSerializer.Serialize(contenido, OpcionesJson);
        return new ResultadoToolLoopPrueba(codigoComando, contenidoJson);
    }

    private static bool EsMensajeRazonamientoReportado(DTOOpenRouterMensajePrueba mensaje)
    {
        return mensaje.Role == "assistant"
            && mensaje.Content?.StartsWith(TituloRazonamientoReportado, StringComparison.Ordinal) == true;
    }

    private static bool EsMensajeInstruccionContinuidad(DTOOpenRouterMensajePrueba mensaje)
    {
        return mensaje.Role == "user"
            && mensaje.Content?.StartsWith("La herramienta solicitada ya fue ejecutada.", StringComparison.Ordinal) == true;
    }

    private static TrazaLoopToolPrueba CrearEntradaTraza(
        int iteracion,
        DateTime fechaSolicitudUtc,
        DateTime fechaRespuestaUtc,
        DTOOpenRouterChoicePrueba choice,
        DTOOpenRouterToolCallPrueba toolCall,
        ResultadoToolLoopPrueba resultadoTool,
        string responseOpenRouterJson)
    {
        return new TrazaLoopToolPrueba
        {
            PreguntaInicial = PreguntaInicial,
            Iteracion = iteracion,
            ResponseOpenRouterJson = responseOpenRouterJson,
            FechaSolicitudUtc = fechaSolicitudUtc,
            FechaRespuestaUtc = fechaRespuestaUtc,
            FinishReason = choice.FinishReason ?? string.Empty,
            NativeFinishReason = choice.NativeFinishReason ?? string.Empty,
            RazonamientoReportado = string.IsNullOrWhiteSpace(choice.Message.Reasoning)
                ? "sin razonamiento reportado"
                : choice.Message.Reasoning,
            ReasoningDetailsJson = choice.Message.ReasoningDetails.HasValue
                ? JsonSerializer.Serialize(choice.Message.ReasoningDetails.Value, OpcionesJson)
                : "[]",
            ToolSolicitada = toolCall.Function.Name,
            ArgumentosSolicitadosJson = toolCall.Function.Arguments,
            CodigoComando = resultadoTool.CodigoComando,
            ResultadoComandoJson = resultadoTool.ContenidoJson,
            SiguientePasoEsperado = ObtenerSiguientePaso(resultadoTool.CodigoComando)
        };
    }

    private static string CrearMensajeRazonamientoReportado(TrazaLoopToolPrueba entrada)
    {
        StringBuilder builder = new();
        builder.AppendLine(TituloRazonamientoReportado);
        builder.AppendLine($"Iteracion: {entrada.Iteracion}");
        builder.AppendLine($"Fecha respuesta UTC: {entrada.FechaRespuestaUtc:O}");
        builder.AppendLine($"Finish reason: {entrada.FinishReason}");
        builder.AppendLine($"Native finish reason: {entrada.NativeFinishReason}");
        builder.AppendLine("Razonamiento:");
        builder.AppendLine(entrada.RazonamientoReportado);
        builder.AppendLine("Reasoning details:");
        builder.AppendLine(entrada.ReasoningDetailsJson);
        return builder.ToString();
    }

    private static string CrearMensajeContinuidad(TrazaLoopToolPrueba entrada)
    {
        StringBuilder builder = new();
        builder.AppendLine("La herramienta solicitada ya fue ejecutada. Usa este resultado junto con el historial anterior para continuar el flujo.");
        builder.AppendLine($"Comando ejecutado: {entrada.CodigoComando}");
        builder.AppendLine($"Tool solicitada: {entrada.ToolSolicitada}");
        builder.AppendLine("Argumentos solicitados:");
        builder.AppendLine(entrada.ArgumentosSolicitadosJson);
        builder.AppendLine("Resultado del comando:");
        builder.AppendLine(entrada.ResultadoComandoJson);
        builder.AppendLine($"Siguiente paso esperado: {entrada.SiguientePasoEsperado}");
        builder.AppendLine(ObtenerInstruccionContinuacion(entrada.CodigoComando));
        return builder.ToString();
    }

    private static string ObtenerInstruccionContinuacion(string codigoComando)
    {
        if (codigoComando == "pedido consultar")
        {
            return "Continua ahora con cliente_consultar usando el clienteId retornado.";
        }

        if (codigoComando == "cliente consultar")
        {
            return "Continua ahora con envio_consultar usando el envioId retornado.";
        }

        return "Ya se ejecutaron las tres herramientas. Devuelve la respuesta final sin pedir mas herramientas.";
    }

    private static string ObtenerSiguientePaso(string codigoComando)
    {
        return codigoComando switch
        {
            "pedido consultar" => "cliente consultar",
            "cliente consultar" => "envio consultar",
            "envio consultar" => "respuesta final",
            _ => "desconocido"
        };
    }

    private static async Task GuardarTrazaAsync(
        string directorio,
        int iteracion,
        TrazaLoopToolPrueba entradaTraza,
        IReadOnlyList<TrazaLoopToolPrueba> traza,
        string mensajeContinuidad,
        string? respuestaFinal,
        CancellationToken cancellationToken)
    {
        await GuardarArchivoAsync(directorio, iteracion, "trace_message.txt", mensajeContinuidad, cancellationToken);
        await GuardarArchivoAsync(
            directorio,
            iteracion,
            "trace_entry.json",
            JsonSerializer.Serialize(entradaTraza, new JsonSerializerOptions(OpcionesJson) { WriteIndented = true }),
            cancellationToken);
        await GuardarTrazaAcumuladaAsync(directorio, traza, respuestaFinal, cancellationToken);
    }

    private static async Task GuardarTrazaAcumuladaAsync(
        string directorio,
        IReadOnlyList<TrazaLoopToolPrueba> traza,
        string? respuestaFinal,
        CancellationToken cancellationToken)
    {
        object contenido = new
        {
            preguntaInicial = PreguntaInicial,
            respuestaFinal,
            traza
        };
        await File.WriteAllTextAsync(
            Path.Combine(directorio, "trace_acumulado.json"),
            JsonSerializer.Serialize(contenido, new JsonSerializerOptions(OpcionesJson) { WriteIndented = true }),
            Encoding.UTF8,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directorio, "trace_acumulado.md"),
            CrearMarkdownTraza(traza, respuestaFinal),
            Encoding.UTF8,
            cancellationToken);
    }

    private static string CrearMarkdownTraza(IReadOnlyList<TrazaLoopToolPrueba> traza, string? respuestaFinal)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Traza OpenRouter Tool Loop");
        builder.AppendLine();
        builder.AppendLine("## Pregunta Inicial");
        builder.AppendLine(PreguntaInicial);
        foreach (TrazaLoopToolPrueba entrada in traza)
        {
            builder.AppendLine();
            builder.AppendLine($"## Iteracion {entrada.Iteracion}");
            builder.AppendLine($"- Solicitud UTC: {entrada.FechaSolicitudUtc:O}");
            builder.AppendLine($"- Respuesta UTC: {entrada.FechaRespuestaUtc:O}");
            builder.AppendLine($"- Finish reason: {entrada.FinishReason}");
            builder.AppendLine($"- Native finish reason: {entrada.NativeFinishReason}");
            builder.AppendLine($"- Tool solicitada: {entrada.ToolSolicitada}");
            builder.AppendLine($"- Comando ejecutado: {entrada.CodigoComando}");
            builder.AppendLine($"- Siguiente paso esperado: {entrada.SiguientePasoEsperado}");
            builder.AppendLine();
            builder.AppendLine("### Razonamiento Reportado");
            builder.AppendLine(entrada.RazonamientoReportado);
            builder.AppendLine();
            builder.AppendLine("### Response OpenRouter");
            builder.AppendLine("```json");
            builder.AppendLine(FormatearJson(entrada.ResponseOpenRouterJson));
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("### Argumentos Solicitados");
            builder.AppendLine("```json");
            builder.AppendLine(FormatearJson(entrada.ArgumentosSolicitadosJson));
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("### Resultado Comando");
            builder.AppendLine("```json");
            builder.AppendLine(FormatearJson(entrada.ResultadoComandoJson));
            builder.AppendLine("```");
        }

        if (!string.IsNullOrWhiteSpace(respuestaFinal))
        {
            builder.AppendLine();
            builder.AppendLine("## Respuesta Final");
            builder.AppendLine(respuestaFinal);
        }

        return builder.ToString();
    }

    private static string ObtenerCodigoComando(string nombreTool)
    {
        return nombreTool switch
        {
            "pedido_consultar" => "pedido consultar",
            "cliente_consultar" => "cliente consultar",
            "envio_consultar" => "envio consultar",
            _ => throw new InvalidOperationException($"OpenRouter pidio una herramienta desconocida: {nombreTool}.")
        };
    }

    private static LineaComando CrearLineaComando(string nombreTool, JsonElement argumentos)
    {
        if (nombreTool == "pedido_consultar")
        {
            string pedido = LeerParametro(argumentos, "pedido", nombreTool);
            return new LineaComando(["pedido", "consultar", $"--pedido={pedido}"]);
        }

        if (nombreTool == "cliente_consultar")
        {
            string clienteId = LeerParametro(argumentos, "clienteId", nombreTool);
            return new LineaComando(["cliente", "consultar", $"--clienteId={clienteId}"]);
        }

        if (nombreTool == "envio_consultar")
        {
            string envioId = LeerParametro(argumentos, "envioId", nombreTool);
            return new LineaComando(["envio", "consultar", $"--envioId={envioId}"]);
        }

        throw new InvalidOperationException($"OpenRouter pidio una herramienta desconocida: {nombreTool}.");
    }

    private static JsonElement LeerArgumentos(DTOOpenRouterToolCallPrueba toolCall)
    {
        if (string.IsNullOrWhiteSpace(toolCall.Function.Arguments))
        {
            throw new InvalidOperationException($"OpenRouter pidio {toolCall.Function.Name} sin argumentos.");
        }

        using JsonDocument documento = JsonDocument.Parse(toolCall.Function.Arguments);
        return documento.RootElement.Clone();
    }

    private static string LeerParametro(JsonElement argumentos, string nombre, string nombreTool)
    {
        if (argumentos.TryGetProperty(nombre, out JsonElement valor) && valor.ValueKind == JsonValueKind.String)
        {
            string? contenido = valor.GetString();
            if (!string.IsNullOrWhiteSpace(contenido))
            {
                return contenido;
            }
        }

        throw new InvalidOperationException($"OpenRouter pidio {nombreTool} sin el parametro requerido {nombre}.");
    }

    private static string LeerParametroLineaComando(ICollection<Parametro> parametros, string nombre)
    {
        Parametro? parametro = parametros.SingleOrDefault(parametroActual => parametroActual.Nombre == nombre);
        if (parametro is null || string.IsNullOrWhiteSpace(parametro.Valor))
        {
            throw new InvalidOperationException($"El comando no recibio el parametro requerido {nombre}.");
        }

        return parametro.Valor;
    }

    private static string LeerVariableObligatoria(string nombre, string mensaje)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(mensaje);
        }

        return valor;
    }

    private static string CrearDirectorioOpenRouterPrueba()
    {
        string directorio = Path.Combine(
            Path.GetTempPath(),
            $"per_mensajeria_openrouter_loop_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        return directorio;
    }

    private static Task GuardarArchivoAsync(string directorio, int iteracion, string nombreArchivo, string contenido, CancellationToken cancellationToken)
    {
        // Formato para buscar despues: /tmp/per_mensajeria_openrouter_loop_yyyyMMdd_{Guid}
        return File.WriteAllTextAsync(
            Path.Combine(directorio, $"iteracion_{iteracion}_{nombreArchivo}"),
            contenido,
            Encoding.UTF8,
            cancellationToken);
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

    private sealed class ConsultarPedidoComando : ComandoBase<string, ResultadoComando>
    {
        private readonly RegistroLoopToolPrueba registro;
        private string pedido = string.Empty;

        public ConsultarPedidoComando(RegistroLoopToolPrueba registro, ICollection<Parametro> parametros)
        {
            this.registro = registro;
            Preparar(parametros);
        }

        public override void Preparar(ICollection<Parametro> parametros)
        {
            pedido = LeerParametroLineaComando(parametros, "--pedido");
        }

        public override Task<ResultadoComando> EjecutarAsync(string entrada, CancellationToken token = default)
        {
            if (pedido != Pedido)
            {
                return Task.FromResult(ResultadoComando.Fallo($"Pedido no esperado: {pedido}"));
            }

            registro.RegistrarEjecucionInterna("pedido consultar");
            Dictionary<string, string> salida = new()
            {
                ["pedido"] = Pedido,
                ["clienteId"] = ClienteId,
                ["estado"] = EstadoPedido
            };
            return Task.FromResult(ResultadoComando.Exito(salida));
        }
    }

    private sealed class ConsultarClienteComando : ComandoBase<string, ResultadoComando>
    {
        private readonly RegistroLoopToolPrueba registro;
        private string clienteId = string.Empty;

        public ConsultarClienteComando(RegistroLoopToolPrueba registro, ICollection<Parametro> parametros)
        {
            this.registro = registro;
            Preparar(parametros);
        }

        public override void Preparar(ICollection<Parametro> parametros)
        {
            clienteId = LeerParametroLineaComando(parametros, "--clienteId");
        }

        public override Task<ResultadoComando> EjecutarAsync(string entrada, CancellationToken token = default)
        {
            if (clienteId != ClienteId)
            {
                return Task.FromResult(ResultadoComando.Fallo($"Cliente no esperado: {clienteId}"));
            }

            registro.RegistrarEjecucionInterna("cliente consultar");
            Dictionary<string, string> salida = new()
            {
                ["clienteId"] = ClienteId,
                ["cliente"] = "Cliente prueba mensajeria",
                ["segmento"] = "prioritario",
                ["envioId"] = EnvioId
            };
            return Task.FromResult(ResultadoComando.Exito(salida));
        }
    }

    private sealed class ConsultarEnvioComando : ComandoBase<string, ResultadoComando>
    {
        private readonly RegistroLoopToolPrueba registro;
        private string envioId = string.Empty;

        public ConsultarEnvioComando(RegistroLoopToolPrueba registro, ICollection<Parametro> parametros)
        {
            this.registro = registro;
            Preparar(parametros);
        }

        public override void Preparar(ICollection<Parametro> parametros)
        {
            envioId = LeerParametroLineaComando(parametros, "--envioId");
        }

        public override Task<ResultadoComando> EjecutarAsync(string entrada, CancellationToken token = default)
        {
            if (envioId != EnvioId)
            {
                return Task.FromResult(ResultadoComando.Fallo($"Envio no esperado: {envioId}"));
            }

            registro.RegistrarEjecucionInterna("envio consultar");
            Dictionary<string, string> salida = new()
            {
                ["envioId"] = EnvioId,
                ["guia"] = Guia,
                ["transportadora"] = "Mensajeria Test",
                ["estadoEnvio"] = "en ruta"
            };
            return Task.FromResult(ResultadoComando.Exito(salida));
        }
    }

    private sealed class RegistroLoopToolPrueba
    {
        private readonly List<string> comandosEjecutados = [];
        private readonly List<string> ejecucionesInternas = [];
        private readonly List<SnapshotSolicitudPrueba> snapshotsSolicitudes = [];

        public IReadOnlyList<string> ComandosEjecutados => comandosEjecutados;
        public IReadOnlyList<string> EjecucionesInternas => ejecucionesInternas;
        public IReadOnlyList<SnapshotSolicitudPrueba> SnapshotsSolicitudes => snapshotsSolicitudes;

        public void RegistrarComando(string codigoComando)
        {
            comandosEjecutados.Add(codigoComando);
        }

        public void RegistrarEjecucionInterna(string codigoComando)
        {
            ejecucionesInternas.Add(codigoComando);
        }

        public void RegistrarSnapshot(int iteracion, int cantidadMensajesTool, int cantidadMensajesRazonamiento, int cantidadMensajesContinuidad)
        {
            snapshotsSolicitudes.Add(new SnapshotSolicitudPrueba(iteracion, cantidadMensajesTool, cantidadMensajesRazonamiento, cantidadMensajesContinuidad));
        }
    }

    private sealed record ResultadoOpenRouterPrueba(DTOOpenRouterChatRespuestaPrueba Respuesta, string CuerpoRespuestaJson);

    private sealed record ResultadoToolLoopPrueba(string CodigoComando, string ContenidoJson);

    private sealed record SnapshotSolicitudPrueba(int Iteracion, int CantidadMensajesTool, int CantidadMensajesRazonamiento, int CantidadMensajesContinuidad);

    private sealed class TrazaLoopToolPrueba
    {
        public string PreguntaInicial { get; set; } = string.Empty;
        public int Iteracion { get; set; }
        public string ResponseOpenRouterJson { get; set; } = string.Empty;
        public DateTime FechaSolicitudUtc { get; set; }
        public DateTime FechaRespuestaUtc { get; set; }
        public string FinishReason { get; set; } = string.Empty;
        public string NativeFinishReason { get; set; } = string.Empty;
        public string RazonamientoReportado { get; set; } = string.Empty;
        public string ReasoningDetailsJson { get; set; } = string.Empty;
        public string ToolSolicitada { get; set; } = string.Empty;
        public string ArgumentosSolicitadosJson { get; set; } = string.Empty;
        public string CodigoComando { get; set; } = string.Empty;
        public string ResultadoComandoJson { get; set; } = string.Empty;
        public string SiguientePasoEsperado { get; set; } = string.Empty;
    }

    private sealed class DTOOpenRouterChatSolicitudPrueba
    {
        public string Model { get; set; } = string.Empty;
        public decimal Temperature { get; set; }

        [JsonPropertyName("max_completion_tokens")]
        public int MaxCompletionTokens { get; set; }

        [JsonPropertyName("tool_choice")]
        public string ToolChoice { get; set; } = "auto";

        public IReadOnlyList<DTOOpenRouterToolPrueba> Tools { get; set; } = [];
        public IReadOnlyList<DTOOpenRouterMensajePrueba> Messages { get; set; } = [];
    }

    private sealed class DTOOpenRouterMensajePrueba
    {
        public string Role { get; set; } = string.Empty;
        public string? Content { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }

        public string? Reasoning { get; set; }

        [JsonPropertyName("reasoning_details")]
        public JsonElement? ReasoningDetails { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<DTOOpenRouterToolCallPrueba> ToolCalls { get; set; } = [];

        public static DTOOpenRouterMensajePrueba AssistantToolCalls(List<DTOOpenRouterToolCallPrueba> toolCalls)
        {
            return new DTOOpenRouterMensajePrueba
            {
                Role = "assistant",
                ToolCalls = toolCalls
            };
        }

        public static DTOOpenRouterMensajePrueba Tool(string toolCallId, string content)
        {
            return new DTOOpenRouterMensajePrueba
            {
                Role = "tool",
                ToolCallId = toolCallId,
                Content = content
            };
        }


        public static DTOOpenRouterMensajePrueba Assistant(string content)
        {
            return new DTOOpenRouterMensajePrueba
            {
                Role = "assistant",
                Content = content
            };
        }

        public static DTOOpenRouterMensajePrueba User(string content)
        {
            return new DTOOpenRouterMensajePrueba
            {
                Role = "user",
                Content = content
            };
        }
    }

    private sealed class DTOOpenRouterToolPrueba
    {
        public string Type { get; set; } = "function";
        public DTOOpenRouterFuncionPrueba Function { get; set; } = new();
    }

    private sealed class DTOOpenRouterFuncionPrueba
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object?> Parameters { get; set; } = [];
    }

    private sealed class DTOOpenRouterChatRespuestaPrueba
    {
        public string Id { get; set; } = string.Empty;
        public List<DTOOpenRouterChoicePrueba> Choices { get; set; } = [];
        public DTOOpenRouterUsagePrueba? Usage { get; set; }
    }

    private sealed class DTOOpenRouterChoicePrueba
    {
        public int Index { get; set; }
        public DTOOpenRouterMensajePrueba Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("native_finish_reason")]
        public string? NativeFinishReason { get; set; }
    }

    private sealed class DTOOpenRouterToolCallPrueba
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DTOOpenRouterToolCallFuncionPrueba Function { get; set; } = new();
    }

    private sealed class DTOOpenRouterToolCallFuncionPrueba
    {
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    private sealed class DTOOpenRouterUsagePrueba
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }

        [JsonPropertyName("prompt_tokens_details")]
        public JsonElement? PromptTokensDetails { get; set; }

        [JsonPropertyName("completion_tokens_details")]
        public JsonElement? CompletionTokensDetails { get; set; }
    }
}
