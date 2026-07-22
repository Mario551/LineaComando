using System.Globalization;
using System.Text;
using System.Text.Json;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public class MiniMaxOpenRouterAdaptador : IOpenRouterModeloAdaptador
{
    private const string HerramientaConsultaMensajesLineaAnterior = "contexto_consultar_mensajes_linea_anterior";
    private const string PrefijoHerramientaComando = "comando_";
    private const string TipoDecisionComando = "decision_comando";
    private const string TipoDecisionConsultaMensajesLineaAnterior = "decision_consulta_mensajes_linea_anterior";
    private const string TipoResultadoConsultaMensajesLineaAnterior = "resultado_consulta_mensajes_linea_anterior";

    private readonly ConfiguracionMiniMaxOpenRouter configuracion;

    public MiniMaxOpenRouterAdaptador(ConfiguracionMiniMaxOpenRouter configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuracion.Modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuracion.Proveedor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuracion.MaximoTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuracion.MaximoLlamadasCompactacion);
        this.configuracion = configuracion;
    }

    public int MaximoLlamadasCompactacion => configuracion.MaximoLlamadasCompactacion;

    public DTOOpenRouterSolicitudChat CrearSolicitudDecision(SolicitudIntencionContexto solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        IReadOnlyDictionary<string, ComandoContexto> comandosPorHerramienta = CrearMapaComandos(solicitud.Comandos);
        List<DTOOpenRouterMensaje> mensajes =
        [
            new DTOOpenRouterMensaje
            {
                Rol = "system",
                Contenido = CrearPromptSistema()
            }
        ];

        if (solicitud.CompactacionContextoInicial is not null)
        {
            mensajes.Add(new DTOOpenRouterMensaje
            {
                Rol = "system",
                Contenido = FormatearContenidoConFecha(
                    solicitud.CompactacionContextoInicial.FechaCreacion,
                    $"COMPACTACION_CONTEXTO_INICIAL\n{solicitud.CompactacionContextoInicial.Contenido}")
            });
        }

        foreach (MetadataEntradaContextoIA entrada in solicitud.MetadataEntradasContextoIA)
        {
            mensajes.Add(CrearMensajeContexto(entrada, comandosPorHerramienta));
        }

        return CrearSolicitudBase(mensajes, CrearHerramientas(comandosPorHerramienta));
    }

    public ResultadoIntencionContexto InterpretarDecision(
        SolicitudIntencionContexto solicitud,
        ResultadoOpenRouterCliente resultado)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(resultado);

        InformacionTecnicaLlamadaIAContexto metadata = CrearInformacionTecnicaLlamadaIA(
            solicitud.Iteracion,
            "Decidir",
            resultado);

        if (!resultado.Exitoso)
        {
            string error = resultado.Error ?? "OpenRouter no pudo procesar la decision.";
            metadata.Error = error;
            string contenidoError = JsonSerializer.Serialize(new { accion = "error", error });

            if (EsLimiteVentana(resultado))
            {
                return ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                    metadata,
                    JsonSerializer.Serialize(new { accion = "limite_ventana", error }),
                    DeteccionLimiteVentanaContextoTipo.RechazoProveedor);
            }

            return ResultadoIntencionContexto.ConError(metadata, contenidoError, error);
        }

        DTOOpenRouterEleccion? eleccion = resultado.Respuesta?.Elecciones.SingleOrDefault();
        if (eleccion is null)
        {
            return CrearErrorDecision(metadata, "OpenRouter debe devolver exactamente una eleccion.");
        }

        if (string.Equals(eleccion.RazonFinalizacion, "length", StringComparison.OrdinalIgnoreCase))
        {
            return CrearErrorDecision(metadata, "OpenRouter corto la respuesta por limite de tokens de salida.");
        }

        List<DTOOpenRouterLlamadaHerramienta> llamadas = eleccion.Mensaje.LlamadasHerramientas ?? [];
        if (llamadas.Count > 0)
        {
            return InterpretarLlamadaHerramienta(solicitud, llamadas, metadata);
        }

        return InterpretarContenidoTerminal(eleccion.Mensaje.Contenido, metadata);
    }

    public DTOOpenRouterSolicitudChat CrearSolicitudCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        IReadOnlyList<string> fragmentos)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(fragmentos);

        string contenido = JsonSerializer.Serialize(new
        {
            fechaSolicitud = solicitud.Solicitud.FechaMensaje,
            fragmentos
        });

        List<DTOOpenRouterMensaje> mensajes =
        [
            new DTOOpenRouterMensaje
            {
                Rol = "system",
                Contenido = "Compacta el contexto conservando hechos, fechas, decisiones y resultados de herramientas. "
                    + "No inventes informacion. Responde solo JSON con la forma {\"contenido\":\"resumen\"}."
            },
            new DTOOpenRouterMensaje
            {
                Rol = "user",
                Contenido = FormatearContenidoConFecha(solicitud.Solicitud.FechaMensaje, contenido)
            }
        ];

        return CrearSolicitudBase(mensajes, null);
    }

    public ResultadoCompactacionOpenRouter InterpretarCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        ResultadoOpenRouterCliente resultado)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(resultado);

        InformacionTecnicaLlamadaIAContexto metadata = CrearInformacionTecnicaLlamadaIA(
            solicitud.Iteracion,
            "Compactar",
            resultado);

        if (!resultado.Exitoso)
        {
            string error = resultado.Error ?? "OpenRouter no pudo compactar el contexto.";
            metadata.Error = error;
            return ResultadoCompactacionOpenRouter.Fallo(error, metadata, EsLimiteVentana(resultado));
        }

        DTOOpenRouterEleccion? eleccion = resultado.Respuesta?.Elecciones.SingleOrDefault();
        if (eleccion is null)
        {
            return ResultadoCompactacionOpenRouter.Fallo(
                "OpenRouter debe devolver exactamente una eleccion de compactacion.",
                metadata);
        }

        if (string.Equals(eleccion.RazonFinalizacion, "length", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoCompactacionOpenRouter.Fallo(
                "OpenRouter corto la compactacion por limite de tokens de salida.",
                metadata);
        }

        try
        {
            string contenidoRespuesta = LimpiarJson(eleccion.Mensaje.Contenido);
            using JsonDocument documento = JsonDocument.Parse(contenidoRespuesta);
            string contenido = LeerString(documento.RootElement, "contenido");
            metadata.Content = contenidoRespuesta;
            return ResultadoCompactacionOpenRouter.Exito(contenido, metadata);
        }
        catch (Exception excepcion) when (excepcion is JsonException or InvalidOperationException)
        {
            metadata.Error = excepcion.Message;
            return ResultadoCompactacionOpenRouter.Fallo(excepcion.Message, metadata);
        }
    }

    public InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaError(
        int iteracion,
        string accion,
        string error)
    {
        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = configuracion.Proveedor,
            Modelo = configuracion.Modelo,
            Adaptador = nameof(MiniMaxOpenRouterAdaptador),
            Iteracion = iteracion,
            AccionDecidida = accion,
            Error = error
        };
    }

    private DTOOpenRouterSolicitudChat CrearSolicitudBase(
        List<DTOOpenRouterMensaje> mensajes,
        List<DTOOpenRouterHerramienta>? herramientas)
    {
        DTOOpenRouterConfiguracionRazonamiento? razonamiento = CrearConfiguracionRazonamiento();

        return new DTOOpenRouterSolicitudChat
        {
            Modelo = configuracion.Modelo,
            Mensajes = mensajes,
            Herramientas = herramientas,
            EleccionHerramienta = herramientas is null ? null : "auto",
            LlamadasHerramientasParalelas = herramientas is null ? null : false,
            Temperatura = configuracion.Temperatura,
            MaximoTokens = configuracion.MaximoTokens,
            FormatoRespuesta = new DTOOpenRouterFormatoRespuesta(),
            Proveedor = new DTOOpenRouterConfiguracionProveedor
            {
                Solo = [configuracion.Proveedor],
                PermitirAlternativas = false
            },
            Razonamiento = razonamiento
        };
    }

    private DTOOpenRouterConfiguracionRazonamiento? CrearConfiguracionRazonamiento()
    {
        if (configuracion.RazonamientoHabilitado is null
            && configuracion.EsfuerzoRazonamiento is null
            && configuracion.MaximoTokensRazonamiento is null
            && configuracion.ExcluirRazonamiento is null)
        {
            return null;
        }

        return new DTOOpenRouterConfiguracionRazonamiento
        {
            Habilitado = configuracion.RazonamientoHabilitado,
            Esfuerzo = configuracion.EsfuerzoRazonamiento,
            MaximoTokens = configuracion.MaximoTokensRazonamiento,
            Excluir = configuracion.ExcluirRazonamiento
        };
    }

    private string CrearPromptSistema()
    {
        return string.Join(
            '\n',
            "CONFIGURACION_DEL_AGENTE",
            configuracion.PromptAgente,
            string.Empty,
            "PROTOCOLO_TECNICO_OBLIGATORIO",
            "Para ejecutar comandos usa exclusivamente las tools disponibles.",
            $"Para consultar mensajes de lineas anteriores usa exclusivamente la tool {HerramientaConsultaMensajesLineaAnterior}.",
            "Cada consulta recupera un ciclo completo. ciclosHaciaAtras=1 es el ciclo anterior mas reciente; incrementa el valor para retroceder.",
            "Solicita como maximo una tool por iteracion.",
            "No inventes ni asumas el resultado de una tool.",
            "Despues de solicitar una tool espera su mensaje role=tool con el mismo ToolCallID antes de continuar.",
            "Una respuesta terminal no debe contener tool_calls.",
            "La respuesta terminal debe contener unicamente el objeto JSON, sin fechas, etiquetas, Markdown ni texto antes o despues.",
            "Cuando el flujo termine responde solo JSON valido con una de estas formas:",
            "{\"accion\":\"responder\",\"mensajes\":[{\"tipoMensaje\":\"texto\",\"contenido\":\"respuesta\"}]}",
            "{\"accion\":\"no_responder\",\"motivo\":\"motivo\"}.");
    }

    private static IReadOnlyDictionary<string, ComandoContexto> CrearMapaComandos(
        IReadOnlyList<ComandoContexto> comandos)
    {
        Dictionary<string, ComandoContexto> resultado = new(StringComparer.Ordinal);
        foreach (ComandoContexto comando in comandos.Where(comando => comando.Autorizado))
        {
            string nombre = CrearNombreHerramienta(comando.Codigo);
            if (!resultado.TryAdd(nombre, comando))
            {
                throw new InvalidOperationException(
                    $"Los comandos autorizados producen un nombre de tool duplicado: {nombre}.");
            }
        }

        return resultado;
    }

    private static List<DTOOpenRouterHerramienta> CrearHerramientas(
        IReadOnlyDictionary<string, ComandoContexto> comandosPorHerramienta)
    {
        List<DTOOpenRouterHerramienta> herramientas = comandosPorHerramienta
            .Select(par => CrearHerramientaComando(par.Key, par.Value))
            .ToList();
        herramientas.Add(new DTOOpenRouterHerramienta
        {
            Funcion = new DTOOpenRouterFuncion
            {
                Nombre = HerramientaConsultaMensajesLineaAnterior,
                Descripcion = "Consulta un ciclo completo de mensajes y metadata perteneciente a una linea anterior de la misma conversacion.",
                Parametros = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["ciclosHaciaAtras"] = new
                        {
                            type = "integer",
                            minimum = 1,
                            description = "Posicion absoluta del ciclo anterior que se desea consultar."
                        }
                    },
                    required = new[] { "ciclosHaciaAtras" },
                    additionalProperties = false
                })
            }
        });
        return herramientas;
    }

    private static DTOOpenRouterHerramienta CrearHerramientaComando(
        string nombre,
        ComandoContexto comando)
    {
        Dictionary<string, object> propiedades = comando.Parametros.ToDictionary(
            parametro => parametro.Key,
            parametro => (object)new
            {
                type = "string",
                description = parametro.Value
            },
            StringComparer.Ordinal);

        string descripcion = string.Join(
            " ",
            new[] { comando.Descripcion, comando.Alcance, comando.ReglasUso }
                .Where(texto => !string.IsNullOrWhiteSpace(texto)));

        return new DTOOpenRouterHerramienta
        {
            Funcion = new DTOOpenRouterFuncion
            {
                Nombre = nombre,
                Descripcion = descripcion,
                Parametros = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = propiedades,
                    required = comando.Parametros.Keys.ToArray(),
                    additionalProperties = false
                })
            }
        };
    }

    private static DTOOpenRouterMensaje CrearMensajeContexto(
        MetadataEntradaContextoIA entrada,
        IReadOnlyDictionary<string, ComandoContexto> comandosPorHerramienta)
    {
        DTOOpenRouterMensaje mensaje = new()
        {
            Rol = entrada.IDRolContextoIA,
            Contenido = FormatearContenidoConFecha(entrada.FechaEntrada, CrearContenidoContexto(entrada)),
            IDLlamadaHerramienta = entrada.IDRolContextoIA == "tool"
                ? entrada.ToolCallID
                : null
        };

        if (entrada.IDRolContextoIA == "assistant"
            && entrada.IDTipoEntradaContextoIA is TipoDecisionComando or TipoDecisionConsultaMensajesLineaAnterior)
        {
            if (string.IsNullOrWhiteSpace(entrada.ToolCallID))
            {
                throw new InvalidOperationException(
                    $"La entrada assistant {entrada.ID} no contiene ToolCallID.");
            }

            mensaje.LlamadasHerramientas =
            [
                CrearLlamadaHerramientaDesdeEntrada(entrada, comandosPorHerramienta)
            ];
        }

        if (entrada.IDRolContextoIA == "tool" && string.IsNullOrWhiteSpace(entrada.ToolCallID))
        {
            throw new InvalidOperationException(
                $"La entrada tool {entrada.ID} no contiene ToolCallID.");
        }

        if (entrada.InformacionTecnicaLlamadaIA is not null)
        {
            mensaje.Razonamiento = entrada.InformacionTecnicaLlamadaIA.Reasoning;
            mensaje.DetallesRazonamiento = LeerJsonOpcional(entrada.InformacionTecnicaLlamadaIA.ReasoningDetailsJson);
        }

        return mensaje;
    }

    private static DTOOpenRouterLlamadaHerramienta CrearLlamadaHerramientaDesdeEntrada(
        MetadataEntradaContextoIA entrada,
        IReadOnlyDictionary<string, ComandoContexto> comandosPorHerramienta)
    {
        if (entrada.IDTipoEntradaContextoIA == TipoDecisionConsultaMensajesLineaAnterior)
        {
            using JsonDocument documentoConsulta = JsonDocument.Parse(entrada.Contenido ?? string.Empty);
            int ciclosHaciaAtras = LeerEnteroPositivo(documentoConsulta.RootElement, "ciclosHaciaAtras");
            return new DTOOpenRouterLlamadaHerramienta
            {
                ID = entrada.ToolCallID!,
                Funcion = new DTOOpenRouterFuncion
                {
                    Nombre = HerramientaConsultaMensajesLineaAnterior,
                    Argumentos = JsonSerializer.Serialize(new { ciclosHaciaAtras })
                }
            };
        }

        using JsonDocument documento = JsonDocument.Parse(entrada.Contenido ?? string.Empty);
        string codigoComando = LeerString(documento.RootElement, "codigoComando");
        KeyValuePair<string, ComandoContexto> comando = comandosPorHerramienta.SingleOrDefault(
            par => par.Value.Codigo == codigoComando);
        if (string.IsNullOrWhiteSpace(comando.Key))
        {
            throw new InvalidOperationException(
                $"El comando persistido '{codigoComando}' no existe en el catalogo autorizado actual.");
        }

        JsonElement parametros = documento.RootElement.TryGetProperty("parametros", out JsonElement parametrosJson)
            ? parametrosJson
            : JsonSerializer.SerializeToElement(new Dictionary<string, string>());

        return new DTOOpenRouterLlamadaHerramienta
        {
            ID = entrada.ToolCallID!,
            Funcion = new DTOOpenRouterFuncion
            {
                Nombre = comando.Key,
                Argumentos = parametros.GetRawText()
            }
        };
    }

    private ResultadoIntencionContexto InterpretarLlamadaHerramienta(
        SolicitudIntencionContexto solicitud,
        IReadOnlyList<DTOOpenRouterLlamadaHerramienta> llamadas,
        InformacionTecnicaLlamadaIAContexto metadata)
    {
        if (llamadas.Count != 1)
        {
            return CrearErrorDecision(metadata, "OpenRouter debe solicitar una sola tool por iteracion.");
        }

        DTOOpenRouterLlamadaHerramienta llamada = llamadas[0];
        if (string.IsNullOrWhiteSpace(llamada.ID))
        {
            return CrearErrorDecision(metadata, "OpenRouter devolvio una tool sin identificador.");
        }

        string nombre = llamada.Funcion.Nombre;
        string argumentos = string.IsNullOrWhiteSpace(llamada.Funcion.Argumentos)
            ? "{}"
            : llamada.Funcion.Argumentos;

        if (nombre == HerramientaConsultaMensajesLineaAnterior)
        {
            try
            {
                using JsonDocument documento = JsonDocument.Parse(argumentos);
                int ciclosHaciaAtras = LeerEnteroPositivo(documento.RootElement, "ciclosHaciaAtras");
                string contenidoDecision = JsonSerializer.Serialize(new
                {
                    accion = "consultar_mensajes_linea_anterior",
                    ciclosHaciaAtras,
                    toolCallID = llamada.ID
                });
                metadata.Content = contenidoDecision;
                return ResultadoIntencionContexto.ConsultarMensajesLineaAnterior(
                    metadata,
                    contenidoDecision,
                    ciclosHaciaAtras,
                    llamada.ID);
            }
            catch (JsonException excepcion)
            {
                return CrearErrorDecision(
                    metadata,
                    $"OpenRouter devolvio argumentos invalidos para consultar mensajes anteriores: {excepcion.Message}");
            }
            catch (InvalidOperationException excepcion)
            {
                return CrearErrorDecision(metadata, excepcion.Message);
            }
        }

        IReadOnlyDictionary<string, ComandoContexto> comandosPorHerramienta = CrearMapaComandos(solicitud.Comandos);
        if (!comandosPorHerramienta.TryGetValue(nombre, out ComandoContexto? comando))
        {
            return CrearErrorDecision(metadata, $"OpenRouter solicito una tool desconocida: {nombre}.");
        }

        try
        {
            Dictionary<string, string> parametros = LeerParametros(argumentos);
            List<string> faltantes = comando.Parametros.Keys
                .Where(parametro => !parametros.ContainsKey(parametro))
                .ToList();
            if (faltantes.Count > 0)
            {
                return CrearErrorDecision(
                    metadata,
                    $"OpenRouter omitio parametros obligatorios de {comando.Codigo}: {string.Join(", ", faltantes)}.");
            }

            string contenidoDecision = JsonSerializer.Serialize(new
            {
                accion = "comando",
                codigoComando = comando.Codigo,
                parametros,
                toolCallID = llamada.ID
            });
            metadata.Content = contenidoDecision;
            return ResultadoIntencionContexto.PedirComando(
                metadata,
                contenidoDecision,
                comando.Codigo,
                parametros,
                llamada.ID);
        }
        catch (JsonException excepcion)
        {
            return CrearErrorDecision(metadata, $"OpenRouter devolvio argumentos invalidos: {excepcion.Message}");
        }
    }

    private static ResultadoIntencionContexto InterpretarContenidoTerminal(
        string? contenido,
        InformacionTecnicaLlamadaIAContexto metadata)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return CrearErrorDecision(metadata, "OpenRouter no devolvio content ni tool_calls.");
        }

        try
        {
            string contenidoLimpio = LimpiarJson(contenido);
            using JsonDocument documento = JsonDocument.Parse(contenidoLimpio);
            JsonElement raiz = documento.RootElement;
            string accion = LeerString(raiz, "accion").ToLowerInvariant();
            metadata.Content = contenidoLimpio;

            if (accion == "responder")
            {
                List<DTOMensajeSaliente> mensajes = LeerMensajesSalientes(raiz);
                if (mensajes.Count == 0)
                {
                    return CrearErrorDecision(metadata, "La respuesta terminal no contiene mensajes salientes.");
                }

                return ResultadoIntencionContexto.Responder(metadata, contenidoLimpio, mensajes.ToArray());
            }

            if (accion is "no_responder" or "no responder")
            {
                return ResultadoIntencionContexto.NoResponder(metadata, contenidoLimpio);
            }

            return CrearErrorDecision(
                metadata,
                $"OpenRouter devolvio una accion terminal no soportada: {accion}.");
        }
        catch (Exception excepcion) when (excepcion is JsonException or InvalidOperationException)
        {
            return CrearErrorDecision(metadata, excepcion.Message);
        }
    }

    private InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaLlamadaIA(
        int iteracion,
        string accion,
        ResultadoOpenRouterCliente resultado)
    {
        DTOOpenRouterEleccion? eleccion = resultado.Respuesta?.Elecciones.FirstOrDefault();
        DTOOpenRouterMensaje? mensaje = eleccion?.Mensaje;
        DTOOpenRouterUso? uso = resultado.Respuesta?.Uso;

        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = resultado.Respuesta?.Proveedor ?? configuracion.Proveedor,
            Modelo = resultado.Respuesta?.Modelo ?? configuracion.Modelo,
            Adaptador = nameof(MiniMaxOpenRouterAdaptador),
            Iteracion = iteracion,
            AccionDecidida = accion,
            FinishReason = eleccion?.RazonFinalizacion,
            NativeFinishReason = eleccion?.RazonFinalizacionNativa,
            PromptTokens = uso?.TokensPrompt,
            CompletionTokens = uso?.TokensRespuesta,
            ReasoningTokens = ObtenerTokensRazonamiento(uso),
            TotalTokens = uso?.TokensTotales,
            RequestJson = resultado.SolicitudJson,
            ResponseJson = resultado.RespuestaJson,
            Content = mensaje?.Contenido,
            Reasoning = mensaje?.Razonamiento,
            ReasoningDetailsJson = mensaje?.DetallesRazonamiento?.GetRawText(),
            Error = resultado.Error
        };
    }

    private static ResultadoIntencionContexto CrearErrorDecision(
        InformacionTecnicaLlamadaIAContexto metadata,
        string error)
    {
        metadata.Error = error;
        string contenido = JsonSerializer.Serialize(new { accion = "error", error });
        metadata.Content = contenido;
        return ResultadoIntencionContexto.ConError(metadata, contenido, error);
    }

    private static bool EsLimiteVentana(ResultadoOpenRouterCliente resultado)
    {
        return string.Equals(
            resultado.TipoError,
            "context_length_exceeded",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CrearNombreHerramienta(string codigoComando)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoComando);
        StringBuilder nombre = new(PrefijoHerramientaComando);
        bool separadorAnterior = false;

        foreach (char caracter in codigoComando.ToLowerInvariant())
        {
            bool valido = caracter is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (valido)
            {
                nombre.Append(caracter);
                separadorAnterior = false;
            }
            else if (!separadorAnterior)
            {
                nombre.Append('_');
                separadorAnterior = true;
            }
        }

        string resultado = nombre.ToString().TrimEnd('_');
        if (resultado == PrefijoHerramientaComando.TrimEnd('_'))
        {
            throw new InvalidOperationException($"El codigo de comando '{codigoComando}' no produce un nombre de tool valido.");
        }

        return resultado;
    }

    private static Dictionary<string, string> LeerParametros(string argumentos)
    {
        using JsonDocument documento = JsonDocument.Parse(argumentos);
        if (documento.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Los argumentos de la tool deben ser un objeto JSON.");
        }

        Dictionary<string, string> parametros = new(StringComparer.Ordinal);
        foreach (JsonProperty propiedad in documento.RootElement.EnumerateObject())
        {
            parametros[propiedad.Name] = propiedad.Value.ValueKind == JsonValueKind.String
                ? propiedad.Value.GetString() ?? string.Empty
                : propiedad.Value.GetRawText();
        }

        return parametros;
    }

    private static string CrearContenidoContexto(MetadataEntradaContextoIA entrada)
    {
        string contenido = entrada.Contenido ?? string.Empty;
        if (entrada.IDTipoEntradaContextoIA != TipoResultadoConsultaMensajesLineaAnterior
            || !entrada.IDCompactacionContextoIncorporada.HasValue)
        {
            return contenido;
        }

        JsonElement referencia = string.IsNullOrWhiteSpace(contenido)
            ? JsonSerializer.SerializeToElement(new { })
            : JsonSerializer.Deserialize<JsonElement>(contenido);
        return JsonSerializer.Serialize(new
        {
            referencia,
            estadoContexto = "incorporada_en_compactacion",
            idCompactacionContexto = entrada.IDCompactacionContextoIncorporada.Value
        });
    }

    private static int LeerEnteroPositivo(JsonElement raiz, string propiedad)
    {
        if (!raiz.TryGetProperty(propiedad, out JsonElement valor)
            || valor.ValueKind != JsonValueKind.Number
            || !valor.TryGetInt32(out int resultado)
            || resultado <= 0)
        {
            throw new InvalidOperationException(
                $"La tool debe indicar la propiedad entera positiva '{propiedad}'.");
        }

        return resultado;
    }

    private static List<DTOMensajeSaliente> LeerMensajesSalientes(JsonElement raiz)
    {
        List<DTOMensajeSaliente> mensajes = [];
        if (raiz.TryGetProperty("mensajes", out JsonElement mensajesJson)
            && mensajesJson.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement mensajeJson in mensajesJson.EnumerateArray())
            {
                mensajes.Add(new DTOMensajeSaliente
                {
                    TipoMensaje = LeerStringOpcional(mensajeJson, "tipoMensaje") ?? "texto",
                    Contenido = LeerString(mensajeJson, "contenido"),
                    FechaMensaje = DateTime.Now
                });
            }

            return mensajes;
        }

        string? contenido = LeerStringOpcional(raiz, "contenido");
        if (!string.IsNullOrWhiteSpace(contenido))
        {
            mensajes.Add(new DTOMensajeSaliente
            {
                TipoMensaje = "texto",
                Contenido = contenido,
                FechaMensaje = DateTime.Now
            });
        }

        return mensajes;
    }

    private static int? ObtenerTokensRazonamiento(DTOOpenRouterUso? uso)
    {
        if (uso?.TokensRazonamiento is not null)
        {
            return uso.TokensRazonamiento;
        }

        if (uso?.DetallesTokensRespuesta is JsonElement detalles
            && detalles.ValueKind == JsonValueKind.Object
            && detalles.TryGetProperty("reasoning_tokens", out JsonElement tokens)
            && tokens.ValueKind == JsonValueKind.Number)
        {
            return tokens.GetInt32();
        }

        return null;
    }

    private static JsonElement? LeerJsonOpcional(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using JsonDocument documento = JsonDocument.Parse(json);
        return documento.RootElement.Clone();
    }

    private static string FormatearContenidoConFecha(DateTime fecha, string contenido)
    {
        return $"[fecha_creacion={fecha.ToString("O", CultureInfo.InvariantCulture)}]\n{contenido}";
    }

    private static string LimpiarJson(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return string.Empty;
        }

        string limpio = contenido.Trim();
        if (limpio.StartsWith("```", StringComparison.Ordinal))
        {
            int saltoLinea = limpio.IndexOf('\n');
            if (saltoLinea >= 0)
            {
                limpio = limpio[(saltoLinea + 1)..];
            }

            if (limpio.EndsWith("```", StringComparison.Ordinal))
            {
                limpio = limpio[..^3];
            }
        }

        return RemoverPrefijoFecha(limpio.Trim());
    }

    private static string RemoverPrefijoFecha(string contenido)
    {
        const string prefijoFechaCreacion = "[fecha_creacion=";
        const string prefijoAnterior = "[fecha=";
        string prefijo = contenido.StartsWith(prefijoFechaCreacion, StringComparison.Ordinal)
            ? prefijoFechaCreacion
            : prefijoAnterior;
        if (!contenido.StartsWith(prefijo, StringComparison.Ordinal))
        {
            return contenido;
        }

        int cierre = contenido.IndexOf(']');
        if (cierre < prefijo.Length)
        {
            return contenido;
        }

        string fecha = contenido[prefijo.Length..cierre];
        if (!DateTimeOffset.TryParse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return contenido;
        }

        return contenido[(cierre + 1)..].TrimStart();
    }

    private static string LeerString(JsonElement raiz, string propiedad)
    {
        string? valor = LeerStringOpcional(raiz, propiedad);
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException($"La respuesta no contiene la propiedad string requerida '{propiedad}'.");
        }

        return valor;
    }

    private static string? LeerStringOpcional(JsonElement raiz, string propiedad)
    {
        if (raiz.TryGetProperty(propiedad, out JsonElement valor)
            && valor.ValueKind == JsonValueKind.String)
        {
            return valor.GetString();
        }

        return null;
    }
}
