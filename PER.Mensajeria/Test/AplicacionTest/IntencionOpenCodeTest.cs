using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AplicacionTest.Infraestructura;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;
using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace AplicacionTest;

public class IntencionOpenCodeTest
{
    private const string PromptAgentePrueba =
        "Eres un agente de prueba especializado en pedidos.";

    [Fact]
    public void DTOOpenCodeRespuesta_DebeConservarCamposDesconocidos()
    {
        const string json = """
            {
              "info": {
                "id": "msg-1",
                "sessionID": "sesion-1",
                "role": "assistant",
                "providerID": "openrouter",
                "modelID": "minimax/minimax-m3",
                "tokens": {
                  "input": 1,
                  "output": 2,
                  "reasoning": 3,
                  "campo_tokens_nuevo": 4
                },
                "campo_info_nuevo": true
              },
              "parts": [
                {
                  "id": "parte-1",
                  "type": "text",
                  "text": "{\"accion\":\"no_responder\"}",
                  "campo_parte_nuevo": { "valor": 7 }
                }
              ],
              "campo_raiz_nuevo": "conservado"
            }
            """;

        DTOOpenCodeRespuestaMensaje? respuesta =
            JsonSerializer.Deserialize<DTOOpenCodeRespuestaMensaje>(json);

        Assert.NotNull(respuesta);
        Assert.True(
            respuesta.DatosAdicionales?.ContainsKey(
                "campo_raiz_nuevo"));
        Assert.True(
            respuesta.Informacion.DatosAdicionales?.ContainsKey(
                "campo_info_nuevo"));
        Assert.True(
            respuesta.Informacion.Tokens?.DatosAdicionales?.ContainsKey(
                "campo_tokens_nuevo"));
        Assert.True(
            Assert.Single(respuesta.Partes)
                .DatosAdicionales?.ContainsKey(
                    "campo_parte_nuevo"));
    }

    [Fact]
    public void CrearSolicitudDecision_DebeEnviarAgenteContextoOrdenadoYSinModelo()
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();
        SolicitudIntencionContexto solicitud =
            CrearSolicitudIntencion();
        solicitud.DatosIntermedios =
        [
            new DatoIntermedioContexto
            {
                Tipo = "dato_no_duplicable",
                Contenido = "NO_DUPLICAR_ESTE_DATO"
            }
        ];
        solicitud.MetadataEntradasContextoIA =
        [
            CrearEntrada(
                2,
                "tool",
                "resultado_comando",
                "Pedido 54013: despachado"),
            CrearEntrada(
                1,
                "user",
                "mensaje_entrada",
                "Consulta el pedido 54013")
        ];

        DTOOpenCodeMensajeSolicitud resultado =
            adaptador.CrearSolicitudDecision(solicitud);
        string solicitudJson = JsonSerializer.Serialize(resultado);

        Assert.Equal("mensajeria-contexto", resultado.Agente);
        Assert.Contains(
            PromptAgentePrueba,
            resultado.Sistema);
        Assert.Contains(
            "PROTOCOLO_TECNICO_OBLIGATORIO",
            resultado.Sistema);
        Assert.All(
            resultado.Herramientas,
            herramienta => Assert.False(herramienta.Value));
        Assert.Single(resultado.Partes);
        Assert.DoesNotContain(
            "NO_DUPLICAR_ESTE_DATO",
            resultado.Partes[0].Texto);
        Assert.DoesNotContain("\"model\"", solicitudJson);
        Assert.DoesNotContain("\"directory\"", solicitudJson);
        Assert.DoesNotContain("\"providerID\"", solicitudJson);

        using JsonDocument documento =
            JsonDocument.Parse(resultado.Partes[0].Texto);
        List<JsonElement> entradas = documento.RootElement
            .GetProperty("metadataEntradasContextoIA")
            .EnumerateArray()
            .ToList();
        Assert.Equal(2, entradas.Count);
        Assert.Equal(1, entradas[0].GetProperty("Orden").GetInt32());
        Assert.Equal(2, entradas[1].GetProperty("Orden").GetInt32());
        Assert.Equal(
            "2026-07-15T10:01:00",
            entradas[0].GetProperty("FechaEntrada").GetString());
    }

    [Fact]
    public void InterpretarDecision_DebeMapearComandoYMetadataTecnica()
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> respuesta =
            CrearRespuestaExitosa(
                """
                {
                  "accion": "comando",
                  "codigoComando": "pedido consultar",
                  "parametros": { "pedido": "54013" }
                }
                """,
                "msg-assistant-1");

        ResultadoIntencionContexto resultado =
            adaptador.InterpretarDecision(
                CrearSolicitudIntencion(),
                respuesta);

        Assert.Equal(
            AccionContextoTipo.Comando,
            resultado.TipoAccion);
        Assert.Equal(
            "pedido consultar",
            resultado.CodigoComando);
        Assert.Equal(
            "54013",
            resultado.ParametrosComando["pedido"]);
        Assert.Equal(
            "msg-assistant-1",
            resultado.ToolCallID);
        Assert.Equal(
            "openrouter",
            resultado.InformacionTecnicaLlamadaIA.Proveedor);
        Assert.Equal(
            "minimax/minimax-m3",
            resultado.InformacionTecnicaLlamadaIA.Modelo);
        Assert.Equal(
            "razonamiento uno\nrazonamiento dos",
            resultado.InformacionTecnicaLlamadaIA.Reasoning);
        Assert.Equal(
            7,
            resultado.InformacionTecnicaLlamadaIA.ReasoningTokens);
        Assert.Equal(
            37,
            resultado.InformacionTecnicaLlamadaIA.TotalTokens);
        Assert.Contains(
            "razonamiento uno",
            resultado.InformacionTecnicaLlamadaIA
                .ReasoningDetailsJson);
    }

    [Fact]
    public void InterpretarDecision_DebeMapearConsultaAnterior()
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado =
            adaptador.InterpretarDecision(
                CrearSolicitudIntencion(),
                CrearRespuestaExitosa(
                    """
                    {
                      "accion": "consultar_mensajes_linea_anterior",
                      "ciclosHaciaAtras": 2
                    }
                    """,
                    "msg-consulta-1"));

        Assert.Equal(
            AccionContextoTipo.ConsultarMensajesLineaAnterior,
            resultado.TipoAccion);
        Assert.Equal(2, resultado.CiclosHaciaAtras);
        Assert.Equal("msg-consulta-1", resultado.ToolCallID);
    }

    [Theory]
    [InlineData(
        "{\"accion\":\"responder\",\"mensajes\":[{\"tipoMensaje\":\"texto\",\"contenido\":\"Pedido listo\"}]}",
        AccionContextoTipo.Responder)]
    [InlineData(
        "{\"accion\":\"no_responder\",\"motivo\":\"No requiere respuesta\"}",
        AccionContextoTipo.NoResponder)]
    public void InterpretarDecision_DebeMapearResultadosTerminales(
        string contenido,
        AccionContextoTipo accionEsperada)
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado =
            adaptador.InterpretarDecision(
                CrearSolicitudIntencion(),
                CrearRespuestaExitosa(contenido));

        Assert.Equal(accionEsperada, resultado.TipoAccion);
        if (accionEsperada == AccionContextoTipo.Responder)
        {
            Assert.Equal(
                "Pedido listo",
                Assert.Single(resultado.MensajesSalientes).Contenido);
        }
    }

    [Theory]
    [InlineData(
        "{\"accion\":\"comando\",\"codigoComando\":\"inexistente\",\"parametros\":{}}",
        "desconocido")]
    [InlineData(
        "{\"accion\":\"comando\",\"codigoComando\":\"pedido consultar\",\"parametros\":{}}",
        "parametros obligatorios")]
    [InlineData(
        "{\"accion\":\"consultar_mensajes_linea_anterior\",\"ciclosHaciaAtras\":0}",
        "entera positiva")]
    [InlineData("no-es-json", "JSON")]
    [InlineData("", "parte de texto")]
    public void InterpretarDecision_ContenidoInvalido_DebeRetornarError(
        string contenido,
        string textoEsperado)
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();

        ResultadoIntencionContexto resultado =
            adaptador.InterpretarDecision(
                CrearSolicitudIntencion(),
                CrearRespuestaExitosa(contenido));

        Assert.Equal(
            AccionContextoTipo.Error,
            resultado.TipoAccion);
        Assert.Contains(
            textoEsperado,
            resultado.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InterpretarDecision_ErrorVentana_DebeRetornarLimiteReal()
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();
        DTOOpenCodeError errorOpenCode = new()
        {
            Nombre = "ProviderError",
            Datos = JsonSerializer.SerializeToElement(new
            {
                code = "context_length_exceeded",
                message = "context too long"
            })
        };
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> respuesta =
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>.Fallo(
                HttpStatusCode.BadRequest,
                "{}",
                """
                {
                  "name": "ProviderError",
                  "data": {
                    "code": "context_length_exceeded",
                    "message": "context too long"
                  }
                }
                """,
                "context too long",
                "ProviderError",
                errorOpenCode);

        ResultadoIntencionContexto resultado =
            adaptador.InterpretarDecision(
                CrearSolicitudIntencion(),
                respuesta);

        Assert.Equal(
            AccionContextoTipo.LimiteVentanaAlcanzado,
            resultado.TipoAccion);
        Assert.Equal(
            DeteccionLimiteVentanaContextoTipo.RechazoProveedor,
            resultado.DeteccionLimiteVentana);
    }

    [Fact]
    public void InterpretarDecision_SalidaTruncada_DebeRetornarError()
    {
        OpenCodeAgenteAdaptador adaptador = CrearAdaptador();
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> respuesta =
            CrearRespuestaExitosa(
                "{\"accion\":\"no_responder\"}");
        respuesta.Respuesta!.Informacion.RazonFinalizacion = "length";

        ResultadoIntencionContexto resultado =
            adaptador.InterpretarDecision(
                CrearSolicitudIntencion(),
                respuesta);

        Assert.Equal(
            AccionContextoTipo.Error,
            resultado.TipoAccion);
        Assert.Contains(
            "tokens de salida",
            resultado.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenCodeCliente_DebeUsarRutasYContratoSinExponerCredenciales()
    {
        ManejadorHttpSecuenciaPrueba manejador = new();
        manejador.AgregarRespuesta(
            HttpStatusCode.OK,
            """
            {
              "id": "sesion-1",
              "title": "prueba"
            }
            """);
        manejador.AgregarRespuesta(
            HttpStatusCode.OK,
            CrearRespuestaMensajeJson(
                "{\"accion\":\"no_responder\"}"));
        manejador.AgregarRespuesta(
            HttpStatusCode.OK,
            "true");
        manejador.AgregarRespuesta(
            HttpStatusCode.OK,
            "true");
        using HttpClient httpClient = new(manejador)
        {
            BaseAddress = new Uri("http://opencode:4096/")
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        "opencode:clave-secreta")));
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeCliente cliente = new(
            httpClient,
            new LoggerPrueba<OpenCodeCliente>(registroLogger));

        ResultadoOpenCodeCliente<DTOOpenCodeSesion> sesion =
            await cliente.CrearSesionAsync(
                new DTOOpenCodeCrearSesionSolicitud
                {
                    Titulo = "prueba"
                },
                CancellationToken.None);
        DTOOpenCodeMensajeSolicitud solicitudMensaje =
            CrearAdaptador().CrearSolicitudDecision(
                CrearSolicitudIntencion());
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> mensaje =
            await cliente.EnviarMensajeAsync(
                "sesion-1",
                solicitudMensaje,
                CancellationToken.None);
        ResultadoOpenCodeCliente<bool> cancelacion =
            await cliente.AbortarSesionAsync(
                "sesion-1",
                CancellationToken.None);
        ResultadoOpenCodeCliente<bool> eliminacion =
            await cliente.EliminarSesionAsync(
                "sesion-1",
                CancellationToken.None);

        Assert.True(sesion.Exitoso);
        Assert.True(mensaje.Exitoso);
        Assert.True(cancelacion.Exitoso);
        Assert.True(eliminacion.Exitoso);
        Assert.Equal(
            [
                HttpMethod.Post,
                HttpMethod.Post,
                HttpMethod.Post,
                HttpMethod.Delete
            ],
            manejador.Peticiones.Select(peticion => peticion.Metodo));
        Assert.Equal(
            "http://opencode:4096/session",
            manejador.Peticiones[0].Uri.ToString());
        Assert.Equal(
            "http://opencode:4096/session/sesion-1/message",
            manejador.Peticiones[1].Uri.ToString());
        Assert.Equal(
            "http://opencode:4096/session/sesion-1/abort",
            manejador.Peticiones[2].Uri.ToString());
        Assert.Equal(
            "http://opencode:4096/session/sesion-1",
            manejador.Peticiones[3].Uri.ToString());
        Assert.All(
            manejador.Peticiones,
            peticion => Assert.Equal(
                "Basic",
                peticion.Autorizacion?.Scheme));
        Assert.DoesNotContain(
            "clave-secreta",
            mensaje.SolicitudJson);
        Assert.DoesNotContain(
            "\"model\"",
            mensaje.SolicitudJson);
        Assert.DoesNotContain(
            "\"directory\"",
            mensaje.SolicitudJson);
        Assert.DoesNotContain(
            registroLogger.Entradas,
            entrada => entrada.Mensaje.Contains(
                "clave-secreta",
                StringComparison.Ordinal));
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task OpenCodeCliente_JsonInvalido_DebePreservarStatusYCuerpo()
    {
        ManejadorHttpSecuenciaPrueba manejador = new();
        manejador.AgregarRespuesta(
            HttpStatusCode.OK,
            "respuesta-no-json");
        using HttpClient httpClient = new(manejador)
        {
            BaseAddress = new Uri("http://opencode:4096/")
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeCliente cliente = new(
            httpClient,
            new LoggerPrueba<OpenCodeCliente>(registroLogger));

        ResultadoOpenCodeCliente<DTOOpenCodeSesion> resultado =
            await cliente.CrearSesionAsync(
                new DTOOpenCodeCrearSesionSolicitud
                {
                    Titulo = "prueba"
                },
                CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal(HttpStatusCode.OK, resultado.CodigoEstado);
        Assert.Equal(
            "respuesta-no-json",
            resultado.RespuestaJson);
        Assert.Equal("invalid_json", resultado.TipoError);
        registroLogger.AssertContieneError("JSON invalido");
    }

    [Fact]
    public async Task OpenCodeCliente_AgenteInexistente_DebePreservarErrorHttp()
    {
        const string cuerpo = """
            {
              "name": "AgentNotFoundError",
              "data": {
                "message": "Agent mensajeria-contexto not found"
              }
            }
            """;
        ManejadorHttpSecuenciaPrueba manejador = new();
        manejador.AgregarRespuesta(
            HttpStatusCode.NotFound,
            cuerpo);
        using HttpClient httpClient = new(manejador)
        {
            BaseAddress = new Uri("http://opencode:4096/")
        };
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeCliente cliente = new(
            httpClient,
            new LoggerPrueba<OpenCodeCliente>(registroLogger));

        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado =
            await cliente.EnviarMensajeAsync(
                "sesion-1",
                CrearAdaptador().CrearSolicitudDecision(
                    CrearSolicitudIntencion()),
                CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal(HttpStatusCode.NotFound, resultado.CodigoEstado);
        Assert.Equal(cuerpo, resultado.RespuestaJson);
        Assert.Equal("AgentNotFoundError", resultado.TipoError);
        Assert.Contains("not found", resultado.Error);
        registroLogger.AssertContieneError("AgentNotFoundError");
    }

    [Fact]
    public async Task OpenCodeCliente_TimeoutInterno_DebeRetornarErrorTecnico()
    {
        using HttpClient httpClient =
            new(new ManejadorHttpEsperaPrueba())
            {
                BaseAddress = new Uri("http://opencode:4096/"),
                Timeout = TimeSpan.FromMilliseconds(50)
            };
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeCliente cliente = new(
            httpClient,
            new LoggerPrueba<OpenCodeCliente>(registroLogger));

        ResultadoOpenCodeCliente<DTOOpenCodeSesion> resultado =
            await cliente.CrearSesionAsync(
                new DTOOpenCodeCrearSesionSolicitud
                {
                    Titulo = "prueba"
                },
                CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Equal("timeout", resultado.TipoError);
        registroLogger.AssertContieneError("timeout");
    }

    [Fact]
    public async Task OpenCodeCliente_CancelacionExterna_DebePropagarse()
    {
        using HttpClient httpClient =
            new(new ManejadorHttpEsperaPrueba())
            {
                BaseAddress = new Uri("http://opencode:4096/"),
                Timeout = TimeSpan.FromMinutes(1)
            };
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeCliente cliente = new(
            httpClient,
            new LoggerPrueba<OpenCodeCliente>(registroLogger));
        using CancellationTokenSource cancelacion =
            new(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cliente.CrearSesionAsync(
                new DTOOpenCodeCrearSesionSolicitud
                {
                    Titulo = "prueba"
                },
                cancelacion.Token));

        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task Intencion_DebeCrearEnviarYEliminarSesion()
    {
        ClienteOpenCodeSecuenciaPrueba cliente = new();
        cliente.AgregarRespuesta(
            CrearRespuestaExitosa(
                "{\"accion\":\"no_responder\",\"motivo\":\"prueba\"}"));
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenCodeIntencionContextoServicio>(
                registroLogger));

        ResultadoIntencionContexto resultado =
            await servicio.DecidirAsync(
                CrearSolicitudIntencion(),
                CancellationToken.None);

        Assert.Equal(
            AccionContextoTipo.NoResponder,
            resultado.TipoAccion);
        Assert.Equal(1, cliente.SesionesCreadas);
        Assert.Equal(1, cliente.MensajesEnviados);
        Assert.Equal(0, cliente.SesionesAbortadas);
        Assert.Equal(1, cliente.SesionesEliminadas);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task Intencion_FalloMensaje_DebeAbortarYEliminarSesion()
    {
        ClienteOpenCodeSecuenciaPrueba cliente = new();
        cliente.AgregarRespuesta(
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>.Fallo(
                HttpStatusCode.InternalServerError,
                "{}",
                "{\"error\":\"fallo\"}",
                "fallo OpenCode",
                "server_error"));
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenCodeIntencionContextoServicio>(
                registroLogger));

        ResultadoIntencionContexto resultado =
            await servicio.DecidirAsync(
                CrearSolicitudIntencion(),
                CancellationToken.None);

        Assert.Equal(
            AccionContextoTipo.Error,
            resultado.TipoAccion);
        Assert.Equal(1, cliente.SesionesAbortadas);
        Assert.Equal(1, cliente.SesionesEliminadas);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task Intencion_FalloAbortar_DebeIntentarEliminarSesion()
    {
        ClienteOpenCodeSecuenciaPrueba cliente = new()
        {
            FallarAbortar = true
        };
        cliente.AgregarRespuesta(
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>.Fallo(
                HttpStatusCode.InternalServerError,
                "{}",
                "{\"error\":\"fallo\"}",
                "fallo OpenCode",
                "server_error"));
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenCodeIntencionContextoServicio>(
                registroLogger));

        ResultadoIntencionContexto resultado =
            await servicio.DecidirAsync(
                CrearSolicitudIntencion(),
                CancellationToken.None);

        Assert.Equal(
            AccionContextoTipo.Error,
            resultado.TipoAccion);
        Assert.Equal(1, cliente.SesionesAbortadas);
        Assert.Equal(1, cliente.SesionesEliminadas);
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task Compactar_LimiteInicial_DebeCompactarJerarquicamente()
    {
        ClienteOpenCodeSecuenciaPrueba cliente = new();
        cliente.AgregarRespuesta(CrearFalloLimiteVentana());
        cliente.AgregarRespuesta(
            CrearRespuestaCompactacion("resumen uno"));
        cliente.AgregarRespuesta(
            CrearRespuestaCompactacion("resumen dos"));
        cliente.AgregarRespuesta(
            CrearRespuestaCompactacion("resumen final"));
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenCodeIntencionContextoServicio>(
                registroLogger));
        SolicitudCompactacionIntencionContexto solicitud =
            CrearSolicitudCompactacion();

        ResultadoCompactacionIntencionContexto resultado =
            await servicio.CompactarAsync(
                solicitud,
                CancellationToken.None);

        Assert.True(resultado.Exitoso);
        Assert.Equal("resumen final", resultado.Contenido);
        Assert.Equal(
            4,
            resultado.InformacionesTecnicasLlamadasIA.Count);
        Assert.Equal(4, cliente.MensajesEnviados);
        Assert.Equal(4, cliente.SesionesEliminadas);
        Assert.All(
            cliente.SolicitudesMensaje,
            mensaje =>
            {
                Assert.Equal(
                    "mensajeria-contexto",
                    mensaje.Agente);
                Assert.All(
                    mensaje.Herramientas,
                    herramienta => Assert.False(
                        herramienta.Value));
            });
        registroLogger.AssertSinErrores();
    }

    [Fact]
    public async Task Compactar_EntradaIndividualExcedeVentana_DebeFallarSinDividirla()
    {
        ClienteOpenCodeSecuenciaPrueba cliente = new();
        cliente.AgregarRespuesta(CrearFalloLimiteVentana());
        RegistroLoggerPrueba registroLogger = new();
        OpenCodeIntencionContextoServicio servicio = new(
            cliente,
            CrearAdaptador(),
            new LoggerPrueba<OpenCodeIntencionContextoServicio>(
                registroLogger));
        SolicitudCompactacionIntencionContexto solicitud =
            CrearSolicitudCompactacion();
        solicitud.MetadataEntradasContextoIA =
        [
            solicitud.MetadataEntradasContextoIA[0]
        ];

        ResultadoCompactacionIntencionContexto resultado =
            await servicio.CompactarAsync(
                solicitud,
                CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Contains(
            "entrada individual",
            resultado.Error,
            StringComparison.OrdinalIgnoreCase);
        Assert.Single(cliente.SolicitudesMensaje);
        registroLogger.AssertSinErrores();
    }

    private static OpenCodeAgenteAdaptador CrearAdaptador()
    {
        return new OpenCodeAgenteAdaptador(
            new ConfiguracionIntencionOpenCode(
                PromptAgentePrueba,
                "mensajeria-contexto")
            {
                Servidor = new Uri("http://opencode:4096"),
                AutenticacionBasica =
                    new ConfiguracionAutenticacionBasicaOpenCode(
                        "opencode",
                        "clave-secreta")
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
                CrearEntrada(
                    1,
                    "user",
                    "mensaje_entrada",
                    "Consulta el pedido 54013")
            ]
        };
    }

    private static SolicitudContextoConversacion CrearSolicitudContexto()
    {
        return new SolicitudContextoConversacion
        {
            IDProcesamientoInternoMensaje = 1,
            IDsProcesamientosInternosMensaje = [1],
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
            FechaEntrada = new DateTime(
                2026,
                7,
                15,
                10,
                orden,
                0)
        };
    }

    private static SolicitudCompactacionIntencionContexto
        CrearSolicitudCompactacion()
    {
        return new SolicitudCompactacionIntencionContexto
        {
            Solicitud = CrearSolicitudContexto(),
            Iteracion = 2,
            MetadataEntradasContextoIA =
            [
                CrearEntrada(
                    1,
                    "user",
                    "mensaje_entrada",
                    "entrada uno"),
                CrearEntrada(
                    2,
                    "assistant",
                    "respuesta_final",
                    "entrada dos")
            ]
        };
    }

    private static ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>
        CrearRespuestaExitosa(
            string contenido,
            string idMensaje = "msg-assistant")
    {
        DTOOpenCodeRespuestaMensaje respuesta = new()
        {
            Informacion = new DTOOpenCodeMensajeAsistente
            {
                ID = idMensaje,
                IDSesion = "sesion-1",
                Rol = "assistant",
                IDProveedor = "openrouter",
                IDModelo = "minimax/minimax-m3",
                RazonFinalizacion = "stop",
                Tokens = new DTOOpenCodeTokens
                {
                    Entrada = 10,
                    Salida = 20,
                    Razonamiento = 7
                }
            },
            Partes =
            [
                new DTOOpenCodeParte
                {
                    ID = "reasoning-1",
                    Tipo = "reasoning",
                    Texto = "razonamiento uno"
                },
                new DTOOpenCodeParte
                {
                    ID = "reasoning-2",
                    Tipo = "reasoning",
                    Texto = "razonamiento dos"
                },
                new DTOOpenCodeParte
                {
                    ID = "finish-1",
                    Tipo = "step-finish",
                    Razon = "stop"
                },
                new DTOOpenCodeParte
                {
                    ID = "text-1",
                    Tipo = "text",
                    Texto = contenido
                }
            ]
        };

        return ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>.Exito(
            HttpStatusCode.OK,
            respuesta,
            "{\"request\":true}",
            JsonSerializer.Serialize(respuesta));
    }

    private static ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>
        CrearRespuestaCompactacion(string contenido)
    {
        return CrearRespuestaExitosa(
            JsonSerializer.Serialize(new
            {
                contenido
            }));
    }

    private static ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>
        CrearFalloLimiteVentana()
    {
        DTOOpenCodeError error = new()
        {
            Nombre = "ProviderError",
            Datos = JsonSerializer.SerializeToElement(new
            {
                code = "context_length_exceeded",
                message = "context too long"
            })
        };
        return ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>.Fallo(
            HttpStatusCode.BadRequest,
            "{}",
            JsonSerializer.Serialize(error),
            "context too long",
            "ProviderError",
            error);
    }

    private static string CrearRespuestaMensajeJson(
        string contenido)
    {
        return JsonSerializer.Serialize(new
        {
            info = new
            {
                id = "msg-1",
                sessionID = "sesion-1",
                role = "assistant",
                providerID = "openrouter",
                modelID = "minimax/minimax-m3",
                finish = "stop",
                tokens = new
                {
                    input = 1,
                    output = 2,
                    reasoning = 0
                }
            },
            parts = new[]
            {
                new
                {
                    id = "parte-1",
                    type = "text",
                    text = contenido
                }
            }
        });
    }

    private sealed record PeticionHttpPrueba(
        HttpMethod Metodo,
        Uri Uri,
        AuthenticationHeaderValue? Autorizacion,
        string Cuerpo);

    private sealed class ManejadorHttpSecuenciaPrueba
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> respuestas = new();

        public List<PeticionHttpPrueba> Peticiones { get; } = [];

        public void AgregarRespuesta(
            HttpStatusCode codigoEstado,
            string contenido)
        {
            respuestas.Enqueue(new HttpResponseMessage(codigoEstado)
            {
                Content = new StringContent(
                    contenido,
                    Encoding.UTF8,
                    "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string cuerpo = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(
                    cancellationToken);
            Peticiones.Add(new PeticionHttpPrueba(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization,
                cuerpo));
            return respuestas.Dequeue();
        }
    }

    private sealed class ManejadorHttpEsperaPrueba
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
            throw new InvalidOperationException(
                "La espera HTTP debio cancelarse.");
        }
    }

    private sealed class ClienteOpenCodeSecuenciaPrueba
        : IOpenCodeCliente
    {
        private readonly Queue<
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>>
            respuestas = new();
        private int secuenciaSesion;

        public int SesionesCreadas { get; private set; }
        public int MensajesEnviados { get; private set; }
        public int SesionesAbortadas { get; private set; }
        public int SesionesEliminadas { get; private set; }
        public bool FallarAbortar { get; init; }
        public List<DTOOpenCodeMensajeSolicitud> SolicitudesMensaje
            { get; } = [];

        public void AgregarRespuesta(
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>
                respuesta)
        {
            respuestas.Enqueue(respuesta);
        }

        public Task<ResultadoOpenCodeCliente<DTOOpenCodeSesion>>
            CrearSesionAsync(
                DTOOpenCodeCrearSesionSolicitud solicitud,
                CancellationToken cancellationToken)
        {
            SesionesCreadas++;
            secuenciaSesion++;
            DTOOpenCodeSesion sesion = new()
            {
                ID = $"sesion-{secuenciaSesion}",
                Titulo = solicitud.Titulo
            };
            return Task.FromResult(
                ResultadoOpenCodeCliente<DTOOpenCodeSesion>.Exito(
                    HttpStatusCode.OK,
                    sesion,
                    JsonSerializer.Serialize(solicitud),
                    JsonSerializer.Serialize(sesion)));
        }

        public Task<
            ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>>
            EnviarMensajeAsync(
                string idSesion,
                DTOOpenCodeMensajeSolicitud solicitud,
                CancellationToken cancellationToken)
        {
            MensajesEnviados++;
            SolicitudesMensaje.Add(solicitud);
            return Task.FromResult(respuestas.Dequeue());
        }

        public Task<ResultadoOpenCodeCliente<bool>>
            AbortarSesionAsync(
                string idSesion,
                CancellationToken cancellationToken)
        {
            SesionesAbortadas++;
            if (FallarAbortar)
            {
                return Task.FromResult(
                    ResultadoOpenCodeCliente<bool>.Fallo(
                        HttpStatusCode.InternalServerError,
                        string.Empty,
                        "{\"error\":\"fallo abortar\"}",
                        "fallo abortar",
                        "server_error"));
            }

            return Task.FromResult(
                ResultadoOpenCodeCliente<bool>.Exito(
                    HttpStatusCode.OK,
                    true,
                    string.Empty,
                    "true"));
        }

        public Task<ResultadoOpenCodeCliente<bool>>
            EliminarSesionAsync(
                string idSesion,
                CancellationToken cancellationToken)
        {
            SesionesEliminadas++;
            return Task.FromResult(
                ResultadoOpenCodeCliente<bool>.Exito(
                    HttpStatusCode.OK,
                    true,
                    string.Empty,
                    "true"));
        }
    }
}
