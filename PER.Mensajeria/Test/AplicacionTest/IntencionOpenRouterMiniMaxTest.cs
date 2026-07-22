using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AplicacionTest.Infraestructura;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;
using PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

namespace AplicacionTest;

public class IntencionOpenRouterMiniMaxTest
{
    private const string PromptAgentePrueba = "Eres un agente de prueba especializado en pedidos.";

    [Fact]
    public void CrearSolicitudDecision_DebeCrearToolsNativasYMensajesConFecha()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        ConfiguracionMiniMaxOpenRouter configuracion = new(PromptAgentePrueba);

        DTOOpenRouterSolicitudChat resultado = adaptador.CrearSolicitudDecision(CrearSolicitudIntencion());

        Assert.Equal("minimax/minimax-m3", resultado.Modelo);
        Assert.Equal(1_000_000, configuracion.LimiteVentanaTokens);
        Assert.False(resultado.LlamadasHerramientasParalelas);
        Assert.Equal(30000, resultado.MaximoTokens);
        Assert.Equal("auto", resultado.EleccionHerramienta);
        Assert.Equal(["minimax"], resultado.Proveedor?.Solo);
        Assert.Equal(2, resultado.Herramientas?.Count);
        Assert.Equal("comando_pedido_consultar", resultado.Herramientas![0].Funcion.Nombre);
        Assert.Equal("contexto_consultar_mensajes_linea_anterior", resultado.Herramientas[1].Funcion.Nombre);
        Assert.Equal(
            "integer",
            resultado.Herramientas[1].Funcion.Parametros!.Value.GetProperty("properties")
                .GetProperty("ciclosHaciaAtras")
                .GetProperty("type")
                .GetString());
        DTOOpenRouterMensaje mensajeSistema = resultado.Mensajes[0];
        Assert.Equal("system", mensajeSistema.Rol);
        Assert.Contains(PromptAgentePrueba, mensajeSistema.Contenido);
        Assert.Contains("PROTOCOLO_TECNICO_OBLIGATORIO", mensajeSistema.Contenido);
        Assert.Contains("una tool por iteracion", mensajeSistema.Contenido);
        Assert.Contains("contexto_consultar_mensajes_linea_anterior", mensajeSistema.Contenido);
        Assert.Contains("sin fechas, etiquetas, Markdown ni texto", mensajeSistema.Contenido);
        DTOOpenRouterMensaje mensajeUsuario = Assert.Single(resultado.Mensajes, mensaje => mensaje.Rol == "user");
        Assert.Contains("[fecha_creacion=", mensajeUsuario.Contenido);
        Assert.Contains("54013", mensajeUsuario.Contenido);
    }

    [Fact]
    public void DTOOpenRouterRespuestaChat_DebeConservarErroresYCamposDesconocidos()
    {
        const string json = """
            {
              "id": "gen-1",
              "model": "minimax/minimax-m3",
              "campo_raiz_nuevo": { "valor": 7 },
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": null },
                  "finish_reason": "error",
                  "error": {
                    "code": 400,
                    "message": "context too long",
                    "metadata": { "error_type": "context_length_exceeded" }
                  },
                  "campo_eleccion_nuevo": true
                }
              ]
            }
            """;

        DTOOpenRouterRespuestaChat? respuesta = JsonSerializer.Deserialize<DTOOpenRouterRespuestaChat>(json);

        Assert.NotNull(respuesta);
        Assert.True(respuesta.PropiedadesAdicionales?.ContainsKey("campo_raiz_nuevo"));
        DTOOpenRouterEleccion eleccion = Assert.Single(respuesta.Elecciones);
        Assert.Equal("context too long", eleccion.Error?.Mensaje);
        Assert.Equal(
            "context_length_exceeded",
            eleccion.Error?.Metadata?.GetProperty("error_type").GetString());
        Assert.True(eleccion.PropiedadesAdicionales?.ContainsKey("campo_eleccion_nuevo"));
    }

    [Fact]
    public void InterpretarDecision_DebeMapearToolComandoYConservarIdentificador()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        SolicitudIntencionContexto solicitud = CrearSolicitudIntencion();
        ResultadoOpenRouterCliente respuesta = CrearRespuestaTool(
            "call-pedido-1",
            "comando_pedido_consultar",
            "{\"pedido\":\"54013\"}");

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(solicitud, respuesta);

        Assert.Equal(AccionContextoTipo.Comando, resultado.TipoAccion);
        Assert.Equal("pedido consultar", resultado.CodigoComando);
        Assert.Equal("call-pedido-1", resultado.ToolCallID);
        Assert.Equal("54013", resultado.ParametrosComando["pedido"]);
        Assert.Contains("toolCallID", resultado.ContenidoDecision);
        Assert.Equal("razonamiento", resultado.InformacionTecnicaLlamadaIA.Reasoning);
        Assert.Equal(9, resultado.InformacionTecnicaLlamadaIA.ReasoningTokens);
    }

    [Fact]
    public void InterpretarDecision_DebeMapearConsultaDeCicloAnterior()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            CrearRespuestaTool(
                "call-consulta-1",
                "contexto_consultar_mensajes_linea_anterior",
                "{\"ciclosHaciaAtras\":2}"));

        Assert.Equal(AccionContextoTipo.ConsultarMensajesLineaAnterior, resultado.TipoAccion);
        Assert.Equal(2, resultado.CiclosHaciaAtras);
        Assert.Equal("call-consulta-1", resultado.ToolCallID);
        Assert.Contains("ciclosHaciaAtras", resultado.ContenidoDecision);
    }

    [Fact]
    public void CrearSolicitudDecision_DebeReconstruirToolLoopYReasoningDetails()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        SolicitudIntencionContexto solicitud = CrearSolicitudIntencion();
        solicitud.MetadataEntradasContextoIA =
        [
            solicitud.MetadataEntradasContextoIA[0],
            new MetadataEntradaContextoIA
            {
                ID = 2,
                Orden = 2,
                IDRolContextoIA = "assistant",
                IDTipoEntradaContextoIA = "decision_comando",
                Contenido = "{\"accion\":\"comando\",\"codigoComando\":\"pedido consultar\",\"parametros\":{\"pedido\":\"54013\"}}",
                ToolCallID = "call-pedido-1",
                FechaEntrada = new DateTime(2026, 7, 15, 10, 1, 0),
                InformacionTecnicaLlamadaIA = new InformacionTecnicaLlamadaIAContexto
                {
                    Proveedor = "MiniMax",
                    Modelo = "minimax/minimax-m3",
                    Adaptador = nameof(MiniMaxOpenRouterAdaptador),
                    Reasoning = "razonamiento anterior",
                    ReasoningDetailsJson = "[{\"type\":\"reasoning.text\",\"text\":\"detalle\"}]"
                }
            },
            new MetadataEntradaContextoIA
            {
                ID = 3,
                Orden = 3,
                IDRolContextoIA = "tool",
                IDTipoEntradaContextoIA = "resultado_comando",
                Contenido = "Pedido 54013: despachado",
                ToolCallID = "call-pedido-1",
                FechaEntrada = new DateTime(2026, 7, 15, 10, 2, 0)
            }
        ];

        DTOOpenRouterSolicitudChat resultado = adaptador.CrearSolicitudDecision(solicitud);

        DTOOpenRouterMensaje mensajeAssistant = Assert.Single(
            resultado.Mensajes,
            mensaje => mensaje.Rol == "assistant");
        DTOOpenRouterLlamadaHerramienta llamada = Assert.Single(mensajeAssistant.LlamadasHerramientas!);
        Assert.Equal("call-pedido-1", llamada.ID);
        Assert.Equal("comando_pedido_consultar", llamada.Funcion.Nombre);
        Assert.Equal("razonamiento anterior", mensajeAssistant.Razonamiento);
        Assert.Equal("detalle", mensajeAssistant.DetallesRazonamiento?.EnumerateArray().Single().GetProperty("text").GetString());
        Assert.Null(mensajeAssistant.IDLlamadaHerramienta);

        DTOOpenRouterMensaje mensajeTool = Assert.Single(resultado.Mensajes, mensaje => mensaje.Rol == "tool");
        Assert.Equal("call-pedido-1", mensajeTool.IDLlamadaHerramienta);
        Assert.Contains("despachado", mensajeTool.Contenido);
    }

    [Fact]
    public void CrearSolicitudDecision_DebePreservarOrdenProyectadoDeCiclosAnteriores()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        SolicitudIntencionContexto solicitud = CrearSolicitudIntencion();
        solicitud.MetadataEntradasContextoIA =
        [
            CrearEntrada(1, "user", "mensaje_entrada", "mensaje actual"),
            CrearEntrada(2, "tool", "resultado_consulta_mensajes_linea_anterior", "{\"estado\":\"cargada\"}"),
            CrearEntrada(1, "user", "mensaje_entrada", "mensaje prestado con orden reiniciado"),
            CrearEntrada(2, "assistant", "respuesta_final", "respuesta prestada"),
            CrearEntrada(3, "assistant", "respuesta_final", "razonamiento posterior a la consulta")
        ];
        solicitud.MetadataEntradasContextoIA[1].ToolCallID = "call-consulta-1";

        DTOOpenRouterSolicitudChat resultado = adaptador.CrearSolicitudDecision(solicitud);

        Assert.Equal(
            [
                "mensaje actual",
                "{\"estado\":\"cargada\"}",
                "mensaje prestado con orden reiniciado",
                "respuesta prestada",
                "razonamiento posterior a la consulta"
            ],
            resultado.Mensajes.Skip(1).Select(mensaje => ObtenerContenidoSinFecha(mensaje.Contenido)));
        Assert.All(
            resultado.Mensajes.Skip(1),
            mensaje => Assert.StartsWith("[fecha_creacion=", mensaje.Contenido));
    }

    [Fact]
    public void CrearSolicitudDecision_ConsultaIncorporadaEnCompactacion_DebeEnviarSoloReferenciaMarcada()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        SolicitudIntencionContexto solicitud = CrearSolicitudIntencion();
        MetadataEntradaContextoIA entradaConsulta = CrearEntrada(
            2,
            "tool",
            "resultado_consulta_mensajes_linea_anterior",
            "{\"idLineaConversacion\":30,\"idProcesamientoInternoMensaje\":70,\"estado\":\"cargada\"}");
        entradaConsulta.ToolCallID = "call-consulta-1";
        entradaConsulta.IDCompactacionContextoIncorporada = 55;
        solicitud.MetadataEntradasContextoIA = [solicitud.MetadataEntradasContextoIA[0], entradaConsulta];

        DTOOpenRouterSolicitudChat resultado = adaptador.CrearSolicitudDecision(solicitud);

        DTOOpenRouterMensaje mensajeTool = Assert.Single(resultado.Mensajes, mensaje => mensaje.Rol == "tool");
        string contenido = ObtenerContenidoSinFecha(mensajeTool.Contenido);
        using JsonDocument documento = JsonDocument.Parse(contenido);
        Assert.Equal("incorporada_en_compactacion", documento.RootElement.GetProperty("estadoContexto").GetString());
        Assert.Equal(55, documento.RootElement.GetProperty("idCompactacionContexto").GetInt64());
        Assert.Equal(
            70,
            documento.RootElement.GetProperty("referencia").GetProperty("idProcesamientoInternoMensaje").GetInt64());
    }

    [Fact]
    public void InterpretarDecision_ErrorContexto_DebeRetornarLimiteVentana()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        ResultadoOpenRouterCliente respuesta = ResultadoOpenRouterCliente.Fallo(
            HttpStatusCode.BadRequest,
            "{}",
            "{\"error\":{\"message\":\"context too long\"}}",
            "context too long",
            "context_length_exceeded");

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            respuesta);

        Assert.Equal(AccionContextoTipo.LimiteVentanaAlcanzado, resultado.TipoAccion);
        Assert.Equal(DeteccionLimiteVentanaContextoTipo.RechazoProveedor, resultado.DeteccionLimiteVentana);
        Assert.Equal("context too long", resultado.InformacionTecnicaLlamadaIA.Error);
    }

    [Fact]
    public void InterpretarDecision_ToolDesconocida_DebeRetornarError()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            CrearRespuestaTool("call-1", "comando_inexistente", "{}"));

        Assert.Equal(AccionContextoTipo.Error, resultado.TipoAccion);
        Assert.Contains("tool desconocida", resultado.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InterpretarDecision_MultiplesTools_DebeRetornarError()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        ResultadoOpenRouterCliente respuesta = CrearRespuestaTools(
            ("call-1", "comando_pedido_consultar", "{\"pedido\":\"54013\"}"),
            ("call-2", "contexto_consultar_mensajes_linea_anterior", "{\"ciclosHaciaAtras\":1}"));

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            respuesta);

        Assert.Equal(AccionContextoTipo.Error, resultado.TipoAccion);
        Assert.Contains("una sola tool", resultado.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("no-es-json", "argumentos invalidos")]
    [InlineData("{}", "parametros obligatorios")]
    public void InterpretarDecision_ArgumentosInvalidos_DebeRetornarError(
        string argumentos,
        string textoEsperado)
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            CrearRespuestaTool("call-1", "comando_pedido_consultar", argumentos));

        Assert.Equal(AccionContextoTipo.Error, resultado.TipoAccion);
        Assert.Contains(textoEsperado, resultado.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"ciclosHaciaAtras\":0}")]
    [InlineData("{\"ciclosHaciaAtras\":\"1\"}")]
    public void InterpretarDecision_ConsultaConPosicionInvalida_DebeRetornarError(string argumentos)
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            CrearRespuestaTool(
                "call-consulta-1",
                "contexto_consultar_mensajes_linea_anterior",
                argumentos));

        Assert.Equal(AccionContextoTipo.Error, resultado.TipoAccion);
        Assert.Contains("entera positiva", resultado.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{\"accion\":\"responder\",\"mensajes\":[{\"tipoMensaje\":\"texto\",\"contenido\":\"Pedido listo\"}]}", AccionContextoTipo.Responder)]
    [InlineData("{\"accion\":\"no_responder\",\"motivo\":\"No requiere respuesta\"}", AccionContextoTipo.NoResponder)]
    public void InterpretarDecision_ContenidoTerminal_DebeMapearAccion(
        string contenido,
        AccionContextoTipo accionEsperada)
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            CrearRespuestaTerminal(contenido));

        Assert.Equal(accionEsperada, resultado.TipoAccion);
        if (accionEsperada == AccionContextoTipo.Responder)
        {
            Assert.Equal("Pedido listo", Assert.Single(resultado.MensajesSalientes).Contenido);
        }
    }

    [Fact]
    public void InterpretarDecision_ContenidoTerminalConFecha_DebeMapearAccion()
    {
        MiniMaxOpenRouterAdaptador adaptador = CrearAdaptador();
        string contenido = "[fecha=2026-07-16T17:23:22.6740705-05:00]\n"
            + "{\"accion\":\"responder\",\"mensajes\":[{\"tipoMensaje\":\"texto\",\"contenido\":\"Pedido listo\"}]}";

        ResultadoIntencionContexto resultado = adaptador.InterpretarDecision(
            CrearSolicitudIntencion(),
            CrearRespuestaTerminal(contenido));

        Assert.Equal(AccionContextoTipo.Responder, resultado.TipoAccion);
        Assert.Equal("Pedido listo", Assert.Single(resultado.MensajesSalientes).Contenido);
    }

    [Fact]
    public async Task OpenRouterCliente_DebeUsarContratoHttpSinExponerApiKeyEnJson()
    {
        ManejadorHttpPrueba manejador = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CrearRespuestaTerminalJson(), Encoding.UTF8, "application/json")
        });
        HttpClient httpClient = new(manejador)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "clave-secreta");
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterCliente cliente = new(httpClient, new LoggerPrueba<OpenRouterCliente>(registroLogger));
        DTOOpenRouterSolicitudChat solicitud = CrearAdaptador().CrearSolicitudDecision(CrearSolicitudIntencion());

        ResultadoOpenRouterCliente resultado = await cliente.CompletarChatAsync(solicitud, CancellationToken.None);

        Assert.True(resultado.Exitoso);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", manejador.Uri?.ToString());
        Assert.Equal("Bearer", manejador.Autorizacion?.Scheme);
        Assert.Equal("clave-secreta", manejador.Autorizacion?.Parameter);
        Assert.DoesNotContain("clave-secreta", resultado.SolicitudJson);
        Assert.DoesNotContain(
            registroLogger.Entradas,
            entrada => entrada.Mensaje.Contains("clave-secreta", StringComparison.Ordinal));
        Assert.Contains("\"parallel_tool_calls\":false", resultado.SolicitudJson);
        Assert.DoesNotContain("\"require_parameters\":true", resultado.SolicitudJson);
        registroLogger.AssertSinErrores();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenRouterCliente_ErrorConHttp200_DebeRetornarFallo(
        bool errorEnEleccion)
    {
        string cuerpo = errorEnEleccion
            ? CrearRespuestaErrorEleccionJson()
            : CrearRespuestaErrorRaizJson();
        ManejadorHttpPrueba manejador = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        });
        using HttpClient httpClient = new(manejador)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterCliente cliente = new(httpClient, new LoggerPrueba<OpenRouterCliente>(registroLogger));

        ResultadoOpenRouterCliente resultado = await cliente.CompletarChatAsync(
            CrearAdaptador().CrearSolicitudDecision(CrearSolicitudIntencion()),
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal(HttpStatusCode.OK, resultado.CodigoEstado);
        Assert.Equal(cuerpo, resultado.RespuestaJson);
        Assert.Equal("context_length_exceeded", resultado.TipoError);
        Assert.Equal("context too long", resultado.Error);
        registroLogger.AssertContieneError("context too long");
    }

    [Fact]
    public async Task OpenRouterCliente_JsonInvalido_DebeConservarStatusYCuerpo()
    {
        const string cuerpo = "{json-invalido";
        ManejadorHttpPrueba manejador = new(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        });
        using HttpClient httpClient = new(manejador)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterCliente cliente = new(httpClient, new LoggerPrueba<OpenRouterCliente>(registroLogger));

        ResultadoOpenRouterCliente resultado = await cliente.CompletarChatAsync(
            CrearAdaptador().CrearSolicitudDecision(CrearSolicitudIntencion()),
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal(HttpStatusCode.BadGateway, resultado.CodigoEstado);
        Assert.Equal(cuerpo, resultado.RespuestaJson);
        Assert.Equal("invalid_json", resultado.TipoError);
        registroLogger.AssertContieneError("JSON invalido");
    }

    [Fact]
    public async Task OpenRouterCliente_Timeout_DebeRetornarFallo()
    {
        using HttpClient httpClient = new(new ManejadorHttpEsperaPrueba())
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
            Timeout = TimeSpan.FromMilliseconds(20)
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterCliente cliente = new(httpClient, new LoggerPrueba<OpenRouterCliente>(registroLogger));

        ResultadoOpenRouterCliente resultado = await cliente.CompletarChatAsync(
            CrearAdaptador().CrearSolicitudDecision(CrearSolicitudIntencion()),
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal("timeout", resultado.TipoError);
        registroLogger.AssertContieneError("timeout");
    }

    [Fact]
    public async Task OpenRouterCliente_CancelacionExterna_DebePropagarCancelacion()
    {
        using HttpClient httpClient = new(new ManejadorHttpEsperaPrueba())
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
            Timeout = TimeSpan.FromMinutes(1)
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterCliente cliente = new(httpClient, new LoggerPrueba<OpenRouterCliente>(registroLogger));
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.CompletarChatAsync(
            CrearAdaptador().CrearSolicitudDecision(CrearSolicitudIntencion()),
            cancellationTokenSource.Token));

        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task CompactarAsync_RechazoVentana_DebeDividirYConservarInformacionTecnicaPorLlamada()
    {
        ClienteOpenRouterSecuenciaPrueba cliente = new();
        cliente.AgregarFalloLimite();
        cliente.AgregarCompactacionExitosa("resumen izquierdo");
        cliente.AgregarCompactacionExitosa("resumen derecho");
        cliente.AgregarCompactacionExitosa("resumen final");
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenRouterIntencionContextoServicio>(registroLogger));
        SolicitudCompactacionIntencionContexto solicitud = new()
        {
            Solicitud = CrearSolicitudContexto(),
            Iteracion = 3,
            MetadataEntradasContextoIA =
            [
                CrearEntrada(1, "user", "mensaje_entrada", "mensaje uno"),
                CrearEntrada(2, "assistant", "respuesta_final", "respuesta dos")
            ]
        };

        ResultadoCompactacionIntencionContexto resultado = await servicio.CompactarAsync(
            solicitud,
            CancellationToken.None);

        Assert.True(resultado.Exitoso);
        Assert.Equal("resumen final", resultado.Contenido);
        Assert.Equal(4, resultado.InformacionesTecnicasLlamadasIA.Count);
        Assert.Equal(4, cliente.Solicitudes.Count);
        Assert.All(resultado.InformacionesTecnicasLlamadasIA, metadata => Assert.Equal(nameof(MiniMaxOpenRouterAdaptador), metadata.Adaptador));
        Assert.All(cliente.Solicitudes, solicitudCompactacion =>
        {
            string? promptCompactacion = solicitudCompactacion.Mensajes[0].Contenido;
            Assert.Contains("Compacta el contexto", promptCompactacion);
            Assert.DoesNotContain(PromptAgentePrueba, promptCompactacion);
            Assert.Null(solicitudCompactacion.Herramientas);
            Assert.Null(solicitudCompactacion.EleccionHerramienta);
            Assert.Null(solicitudCompactacion.LlamadasHerramientasParalelas);
        });
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task CompactarAsync_EntradaIndividualExcedeVentana_DebeFallarSinDividirTexto()
    {
        ClienteOpenRouterSecuenciaPrueba cliente = new();
        cliente.AgregarFalloLimite();
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenRouterIntencionContextoServicio>(registroLogger));
        SolicitudCompactacionIntencionContexto solicitud = new()
        {
            Solicitud = CrearSolicitudContexto(),
            Iteracion = 3,
            MetadataEntradasContextoIA = [CrearEntrada(1, "user", "mensaje_entrada", "entrada indivisible")]
        };

        ResultadoCompactacionIntencionContexto resultado = await servicio.CompactarAsync(
            solicitud,
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Contains("entrada individual", resultado.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(resultado.InformacionesTecnicasLlamadasIA);
        Assert.Single(cliente.Solicitudes);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task CompactarAsync_MaximoLlamadas_DebeDetenerRecursion()
    {
        ClienteOpenRouterSecuenciaPrueba cliente = new();
        cliente.AgregarFalloLimite();
        ConfiguracionMiniMaxOpenRouter configuracion = new(PromptAgentePrueba)
        {
            MaximoLlamadasCompactacion = 1
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenRouterIntencionContextoServicio servicio = new(
            cliente,
            new MiniMaxOpenRouterAdaptador(configuracion),
            new LoggerPrueba<OpenRouterIntencionContextoServicio>(registroLogger));
        SolicitudCompactacionIntencionContexto solicitud = new()
        {
            Solicitud = CrearSolicitudContexto(),
            Iteracion = 3,
            MetadataEntradasContextoIA =
            [
                CrearEntrada(1, "user", "mensaje_entrada", "entrada uno"),
                CrearEntrada(2, "assistant", "respuesta_final", "entrada dos")
            ]
        };

        ResultadoCompactacionIntencionContexto resultado = await servicio.CompactarAsync(
            solicitud,
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Contains("maximo de llamadas", resultado.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(resultado.InformacionesTecnicasLlamadasIA);
        Assert.Single(cliente.Solicitudes);
        registroLogger.AssertSinErrores();
    }

    private static MiniMaxOpenRouterAdaptador CrearAdaptador()
    {
        return new MiniMaxOpenRouterAdaptador(new ConfiguracionMiniMaxOpenRouter(PromptAgentePrueba)
        {
            Temperatura = 0,
            MaximoLlamadasCompactacion = 10
        });
    }

    private static SolicitudIntencionContexto CrearSolicitudIntencion()
    {
        return new SolicitudIntencionContexto
        {
            Solicitud = CrearSolicitudContexto(),
            Iteracion = 1,
            Comandos =
            [
                new ComandoContexto
                {
                    Codigo = "pedido consultar",
                    Descripcion = "Consulta un pedido",
                    Alcance = "Prueba",
                    ReglasUso = "Requiere pedido",
                    Parametros = new Dictionary<string, string>
                    {
                        ["pedido"] = "Numero de pedido"
                    }
                }
            ],
            MetadataEntradasContextoIA =
            [
                CrearEntrada(1, "user", "mensaje_entrada", "Consulta el pedido 54013")
            ]
        };
    }

    private static SolicitudContextoConversacion CrearSolicitudContexto()
    {
        return new SolicitudContextoConversacion
        {
            IDProcesamientoInternoMensaje = 1,
            IDMensaje = 2,
            IDConversacion = 3,
            IDLineaConversacion = 4,
            IDCuentaCanal = 5,
            TipoMensaje = "texto",
            Contenido = "Consulta el pedido 54013",
            FechaMensaje = new DateTime(2026, 7, 15, 10, 0, 0)
        };
    }

    private static MetadataEntradaContextoIA CrearEntrada(
        int orden,
        string rol,
        string tipo,
        string contenido)
    {
        return new MetadataEntradaContextoIA
        {
            ID = orden,
            IDLineaConversacion = 4,
            IDMensaje = 2,
            IDProcesamientoInternoMensaje = 1,
            Orden = orden,
            IDRolContextoIA = rol,
            IDTipoEntradaContextoIA = tipo,
            Contenido = contenido,
            FechaEntrada = new DateTime(2026, 7, 15, 10, orden, 0)
        };
    }

    private static string ObtenerContenidoSinFecha(string? contenido)
    {
        Assert.False(string.IsNullOrWhiteSpace(contenido));
        int saltoLinea = contenido!.IndexOf('\n');
        Assert.True(saltoLinea >= 0, "El mensaje debe contener la fecha en una linea separada.");
        return contenido[(saltoLinea + 1)..];
    }

    private static ResultadoOpenRouterCliente CrearRespuestaTool(
        string id,
        string nombre,
        string argumentos)
    {
        return CrearRespuestaTools((id, nombre, argumentos));
    }

    private static ResultadoOpenRouterCliente CrearRespuestaTools(
        params (string ID, string Nombre, string Argumentos)[] llamadas)
    {
        DTOOpenRouterRespuestaChat respuesta = new()
        {
            Modelo = "minimax/minimax-m3",
            Proveedor = "MiniMax",
            Elecciones =
            [
                new DTOOpenRouterEleccion
                {
                    RazonFinalizacion = "tool_calls",
                    RazonFinalizacionNativa = "tool_calls",
                    Mensaje = new DTOOpenRouterMensaje
                    {
                        Rol = "assistant",
                        Razonamiento = "razonamiento",
                        DetallesRazonamiento = JsonSerializer.SerializeToElement(new[]
                        {
                            new { type = "reasoning.text", text = "detalle" }
                        }),
                        LlamadasHerramientas = llamadas
                            .Select(llamada => new DTOOpenRouterLlamadaHerramienta
                            {
                                ID = llamada.ID,
                                Funcion = new DTOOpenRouterFuncion
                                {
                                    Nombre = llamada.Nombre,
                                    Argumentos = llamada.Argumentos
                                }
                            })
                            .ToList()
                    }
                }
            ],
            Uso = new DTOOpenRouterUso
            {
                TokensPrompt = 10,
                TokensRespuesta = 20,
                TokensTotales = 30,
                DetallesTokensRespuesta = JsonSerializer.SerializeToElement(new { reasoning_tokens = 9 })
            }
        };

        return ResultadoOpenRouterCliente.Exito(
            HttpStatusCode.OK,
            respuesta,
            "{\"request\":true}",
            "{\"response\":true}");
    }

    private static ResultadoOpenRouterCliente CrearRespuestaTerminal(string contenido)
    {
        DTOOpenRouterRespuestaChat respuesta = new()
        {
            Modelo = "minimax/minimax-m3",
            Proveedor = "MiniMax",
            Elecciones =
            [
                new DTOOpenRouterEleccion
                {
                    RazonFinalizacion = "stop",
                    RazonFinalizacionNativa = "stop",
                    Mensaje = new DTOOpenRouterMensaje
                    {
                        Rol = "assistant",
                        Contenido = contenido
                    }
                }
            ]
        };

        return ResultadoOpenRouterCliente.Exito(
            HttpStatusCode.OK,
            respuesta,
            "{\"request\":true}",
            JsonSerializer.Serialize(respuesta));
    }

    private static string CrearRespuestaTerminalJson()
    {
        return "{\"id\":\"gen-1\",\"model\":\"minimax/minimax-m3\",\"provider\":\"MiniMax\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"native_finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"accion\\\":\\\"no_responder\\\"}\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";
    }

    private static string CrearRespuestaErrorRaizJson()
    {
        return "{\"error\":{\"code\":400,\"message\":\"context too long\",\"metadata\":{\"error_type\":\"context_length_exceeded\"}}}";
    }

    private static string CrearRespuestaErrorEleccionJson()
    {
        return "{\"id\":\"gen-error\",\"choices\":[{\"index\":0,\"finish_reason\":\"error\",\"message\":{\"role\":\"assistant\",\"content\":null},\"error\":{\"code\":400,\"message\":\"context too long\",\"metadata\":{\"error_type\":\"context_length_exceeded\"}}}]}";
    }

    private sealed class ManejadorHttpPrueba : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public ManejadorHttpPrueba(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        public Uri? Uri { get; private set; }
        public AuthenticationHeaderValue? Autorizacion { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Autorizacion = request.Headers.Authorization;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class ManejadorHttpEsperaPrueba : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("La espera HTTP debio cancelarse.");
        }
    }

    private sealed class ClienteOpenRouterSecuenciaPrueba : IOpenRouterCliente
    {
        private readonly Queue<Func<DTOOpenRouterSolicitudChat, ResultadoOpenRouterCliente>> resultados = new();

        public List<DTOOpenRouterSolicitudChat> Solicitudes { get; } = [];

        public void AgregarFalloLimite()
        {
            resultados.Enqueue(solicitud => ResultadoOpenRouterCliente.Fallo(
                HttpStatusCode.BadRequest,
                JsonSerializer.Serialize(solicitud),
                "{\"error\":{\"message\":\"context too long\"}}",
                "context too long",
                "context_length_exceeded"));
        }

        public void AgregarCompactacionExitosa(string contenido)
        {
            resultados.Enqueue(solicitud =>
            {
                DTOOpenRouterRespuestaChat respuesta = new()
                {
                    Modelo = "minimax/minimax-m3",
                    Proveedor = "MiniMax",
                    Elecciones =
                    [
                        new DTOOpenRouterEleccion
                        {
                            RazonFinalizacion = "stop",
                            Mensaje = new DTOOpenRouterMensaje
                            {
                                Rol = "assistant",
                                Contenido = JsonSerializer.Serialize(new { contenido })
                            }
                        }
                    ]
                };
                return ResultadoOpenRouterCliente.Exito(
                    HttpStatusCode.OK,
                    respuesta,
                    JsonSerializer.Serialize(solicitud),
                    JsonSerializer.Serialize(respuesta));
            });
        }

        public Task<ResultadoOpenRouterCliente> CompletarChatAsync(
            DTOOpenRouterSolicitudChat solicitud,
            CancellationToken cancellationToken)
        {
            Solicitudes.Add(solicitud);
            return Task.FromResult(resultados.Dequeue()(solicitud));
        }
    }
}
