using System.Globalization;
using System.Text.Json;
using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class OpenCodeAgenteAdaptador : IOpenCodeAgenteAdaptador
{
    private readonly ConfiguracionIntencionOpenCode configuracion;

    public OpenCodeAgenteAdaptador(ConfiguracionIntencionOpenCode configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        this.configuracion = configuracion;
    }

    public DTOOpenCodeMensajeSolicitud CrearSolicitudDecision(
        SolicitudIntencionContexto solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        string contextoJson = JsonSerializer.Serialize(
            new
            {
                identificadores = new
                {
                    solicitud.Solicitud.IDProcesamientoInternoMensaje,
                    solicitud.Solicitud.IDsProcesamientosInternosMensaje,
                    solicitud.Solicitud.IDMensaje,
                    solicitud.Solicitud.IDConversacion,
                    solicitud.Solicitud.IDLineaConversacion,
                    solicitud.Solicitud.IDCuentaCanal,
                    solicitud.Iteracion
                },
                fechaSolicitud = solicitud.Solicitud.FechaMensaje,
                compactacionContextoInicial = solicitud.CompactacionContextoInicial is null
                    ? null
                    : new
                    {
                        solicitud.CompactacionContextoInicial.ID,
                        solicitud.CompactacionContextoInicial.Version,
                        solicitud.CompactacionContextoInicial.Contenido,
                        solicitud.CompactacionContextoInicial.FechaCreacion
                    },
                comandosAutorizados = solicitud.Comandos
                    .Where(comando => comando.Autorizado)
                    .Select(comando => new
                    {
                        comando.Codigo,
                        comando.Descripcion,
                        comando.Alcance,
                        comando.ReglasUso,
                        comando.Parametros
                    }),
                metadataEntradasContextoIA = solicitud.MetadataEntradasContextoIA
                    .OrderBy(entrada => entrada.Orden)
                    .ThenBy(entrada => entrada.ID)
                    .Select(CrearEntradaContexto)
            },
            OpenCodeSerializacion.Opciones);

        return CrearSolicitudMensaje(
            CrearPromptSistemaDecision(),
            contextoJson);
    }

    public ResultadoIntencionContexto InterpretarDecision(
        SolicitudIntencionContexto solicitud,
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(resultado);

        InformacionTecnicaLlamadaIAContexto informacionTecnica =
            CrearInformacionTecnicaLlamadaIA(
                solicitud.Iteracion,
                "Decidir",
                resultado);

        if (!resultado.Exitoso)
        {
            string error = resultado.Error
                ?? "OpenCode no pudo procesar la decision.";
            informacionTecnica.Error = error;
            if (EsLimiteVentana(resultado))
            {
                return ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                    informacionTecnica,
                    JsonSerializer.Serialize(new
                    {
                        accion = "limite_ventana",
                        error
                    }),
                    DeteccionLimiteVentanaContextoTipo.RechazoProveedor);
            }

            return CrearErrorDecision(informacionTecnica, error);
        }

        DTOOpenCodeRespuestaMensaje? respuesta = resultado.Respuesta;
        if (respuesta is null)
        {
            return CrearErrorDecision(
                informacionTecnica,
                "OpenCode devolvio una respuesta vacia.");
        }

        if (respuesta.Informacion.Error is not null)
        {
            string error = ObtenerMensajeError(respuesta.Informacion.Error)
                ?? ObtenerNombreError(
                    respuesta.Informacion.Error,
                    "OpenCode devolvio un error de inferencia.");
            informacionTecnica.Error = error;
            if (EsLimiteVentana(respuesta.Informacion.Error))
            {
                return ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                    informacionTecnica,
                    JsonSerializer.Serialize(new
                    {
                        accion = "limite_ventana",
                        error
                    }),
                    DeteccionLimiteVentanaContextoTipo.RechazoProveedor);
            }

            return CrearErrorDecision(informacionTecnica, error);
        }

        if (EsSalidaTruncada(respuesta.Informacion))
        {
            return CrearErrorDecision(
                informacionTecnica,
                "OpenCode corto la respuesta por limite de tokens de salida.");
        }

        string contenido = ObtenerContenido(respuesta.Partes);
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return CrearErrorDecision(
                informacionTecnica,
                "OpenCode no devolvio una parte de texto con la decision.");
        }

        return InterpretarContenidoDecision(
            solicitud,
            contenido,
            respuesta.Informacion.ID,
            informacionTecnica);
    }

    public DTOOpenCodeMensajeSolicitud CrearSolicitudCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        IReadOnlyList<string> fragmentos)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(fragmentos);

        string contenido = JsonSerializer.Serialize(
            new
            {
                fechaSolicitud = solicitud.Solicitud.FechaMensaje,
                fragmentos
            },
            OpenCodeSerializacion.Opciones);

        const string promptCompactacion =
            "Compacta el contexto conservando hechos, fechas, decisiones y resultados de herramientas. "
            + "No inventes informacion. Responde unicamente JSON con la forma "
            + "{\"contenido\":\"resumen\"}.";

        return CrearSolicitudMensaje(promptCompactacion, contenido);
    }

    public ResultadoCompactacionOpenCode InterpretarCompactacion(
        SolicitudCompactacionIntencionContexto solicitud,
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(resultado);

        InformacionTecnicaLlamadaIAContexto informacionTecnica =
            CrearInformacionTecnicaLlamadaIA(
                solicitud.Iteracion,
                "Compactar",
                resultado);

        if (!resultado.Exitoso)
        {
            string error = resultado.Error
                ?? "OpenCode no pudo compactar el contexto.";
            informacionTecnica.Error = error;
            return ResultadoCompactacionOpenCode.Fallo(
                error,
                informacionTecnica,
                EsLimiteVentana(resultado));
        }

        DTOOpenCodeRespuestaMensaje? respuesta = resultado.Respuesta;
        if (respuesta is null)
        {
            return CrearErrorCompactacion(
                informacionTecnica,
                "OpenCode devolvio una respuesta de compactacion vacia.");
        }

        if (respuesta.Informacion.Error is not null)
        {
            string error = ObtenerMensajeError(respuesta.Informacion.Error)
                ?? ObtenerNombreError(
                    respuesta.Informacion.Error,
                    "OpenCode devolvio un error al compactar.");
            informacionTecnica.Error = error;
            return ResultadoCompactacionOpenCode.Fallo(
                error,
                informacionTecnica,
                EsLimiteVentana(respuesta.Informacion.Error));
        }

        if (EsSalidaTruncada(respuesta.Informacion))
        {
            return CrearErrorCompactacion(
                informacionTecnica,
                "OpenCode corto la compactacion por limite de tokens de salida.");
        }

        try
        {
            string contenidoRespuesta = LimpiarJson(
                ObtenerContenido(respuesta.Partes));
            using JsonDocument documento =
                JsonDocument.Parse(contenidoRespuesta);
            string contenido = LeerString(
                documento.RootElement,
                "contenido");
            informacionTecnica.Content = contenidoRespuesta;
            return ResultadoCompactacionOpenCode.Exito(
                contenido,
                informacionTecnica);
        }
        catch (Exception excepcion)
            when (excepcion is JsonException or InvalidOperationException)
        {
            return CrearErrorCompactacion(
                informacionTecnica,
                excepcion.Message);
        }
    }

    public InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaError(
        int iteracion,
        string accion,
        string error,
        string? solicitudJson = null,
        string? respuestaJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = "opencode",
            Modelo = $"agente:{configuracion.NombreAgente}",
            Adaptador = nameof(OpenCodeAgenteAdaptador),
            Iteracion = iteracion,
            AccionDecidida = accion,
            RequestJson = solicitudJson,
            ResponseJson = respuestaJson,
            Error = error
        };
    }

    private DTOOpenCodeMensajeSolicitud CrearSolicitudMensaje(
        string sistema,
        string contenido)
    {
        return new DTOOpenCodeMensajeSolicitud
        {
            Agente = configuracion.NombreAgente,
            Sistema = sistema,
            Herramientas = CrearHerramientasDeshabilitadas(),
            Partes =
            [
                new DTOOpenCodeParteEntrada
                {
                    Texto = contenido
                }
            ]
        };
    }

    private string CrearPromptSistemaDecision()
    {
        return string.Join(
            '\n',
            "CONFIGURACION_DEL_AGENTE",
            configuracion.PromptAgente,
            string.Empty,
            "PROTOCOLO_TECNICO_OBLIGATORIO",
            "No ejecutes herramientas de OpenCode ni comandos de negocio.",
            "Decide una unica accion por llamada usando exclusivamente los datos recibidos.",
            "Los comandos disponibles son un catalogo; solicitar un comando no equivale a ejecutarlo.",
            "No inventes resultados de comandos ni de consultas anteriores.",
            "Toda respuesta debe ser un unico objeto JSON valido, sin Markdown ni texto adicional.",
            "Acciones validas:",
            "{\"accion\":\"comando\",\"codigoComando\":\"codigo exacto\",\"parametros\":{\"parametro\":\"valor\"}}",
            "{\"accion\":\"consultar_mensajes_linea_anterior\",\"ciclosHaciaAtras\":1}",
            "{\"accion\":\"responder\",\"mensajes\":[{\"tipoMensaje\":\"texto\",\"contenido\":\"respuesta\"}]}",
            "{\"accion\":\"no_responder\",\"motivo\":\"motivo\"}",
            "{\"accion\":\"limite_ventana\",\"motivo\":\"motivo\"}",
            "{\"accion\":\"error\",\"error\":\"descripcion\"}");
    }

    private static Dictionary<string, bool> CrearHerramientasDeshabilitadas()
    {
        return new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["*"] = false,
            ["bash"] = false,
            ["edit"] = false,
            ["write"] = false,
            ["patch"] = false,
            ["apply_patch"] = false,
            ["read"] = false,
            ["glob"] = false,
            ["grep"] = false,
            ["list"] = false,
            ["task"] = false,
            ["webfetch"] = false,
            ["websearch"] = false,
            ["question"] = false,
            ["skill"] = false,
            ["lsp"] = false,
            ["todowrite"] = false,
            ["todoread"] = false
        };
    }

    private static object CrearEntradaContexto(
        MetadataEntradaContextoIA entrada)
    {
        return new
        {
            entrada.ID,
            entrada.Orden,
            rol = entrada.IDRolContextoIA,
            tipoEntrada = entrada.IDTipoEntradaContextoIA,
            entrada.Contenido,
            entrada.ToolCallID,
            entrada.FechaEntrada,
            entrada.IDCompactacionContextoIncorporada,
            informacionTecnicaLlamadaIA =
                entrada.InformacionTecnicaLlamadaIA is null
                    ? null
                    : new
                    {
                        entrada.InformacionTecnicaLlamadaIA.Proveedor,
                        entrada.InformacionTecnicaLlamadaIA.Modelo,
                        entrada.InformacionTecnicaLlamadaIA.Adaptador,
                        entrada.InformacionTecnicaLlamadaIA.Iteracion,
                        entrada.InformacionTecnicaLlamadaIA.AccionDecidida,
                        entrada.InformacionTecnicaLlamadaIA.FinishReason,
                        entrada.InformacionTecnicaLlamadaIA.NativeFinishReason,
                        entrada.InformacionTecnicaLlamadaIA.PromptTokens,
                        entrada.InformacionTecnicaLlamadaIA.CompletionTokens,
                        entrada.InformacionTecnicaLlamadaIA.ReasoningTokens,
                        entrada.InformacionTecnicaLlamadaIA.TotalTokens,
                        entrada.InformacionTecnicaLlamadaIA.Content,
                        entrada.InformacionTecnicaLlamadaIA.Reasoning,
                        entrada.InformacionTecnicaLlamadaIA.ReasoningDetailsJson,
                        entrada.InformacionTecnicaLlamadaIA.Error
                    }
        };
    }

    private ResultadoIntencionContexto InterpretarContenidoDecision(
        SolicitudIntencionContexto solicitud,
        string contenido,
        string idMensajeAsistente,
        InformacionTecnicaLlamadaIAContexto informacionTecnica)
    {
        try
        {
            string contenidoLimpio = LimpiarJson(contenido);
            using JsonDocument documento =
                JsonDocument.Parse(contenidoLimpio);
            JsonElement raiz = documento.RootElement;
            string accion = LeerString(raiz, "accion").ToLowerInvariant();
            informacionTecnica.Content = contenidoLimpio;

            return accion switch
            {
                "responder" => CrearRespuesta(
                    raiz,
                    contenidoLimpio,
                    informacionTecnica),
                "no_responder" or "no responder" =>
                    ResultadoIntencionContexto.NoResponder(
                        informacionTecnica,
                        contenidoLimpio),
                "comando" => CrearComando(
                    solicitud,
                    raiz,
                    contenidoLimpio,
                    idMensajeAsistente,
                    informacionTecnica),
                "consultar_mensajes_linea_anterior" =>
                    CrearConsultaMensajesLineaAnterior(
                        raiz,
                        contenidoLimpio,
                        idMensajeAsistente,
                        informacionTecnica),
                "limite_ventana" =>
                    ResultadoIntencionContexto.LimiteVentanaAlcanzado(
                        informacionTecnica,
                        contenidoLimpio,
                        DeteccionLimiteVentanaContextoTipo.Estimado),
                "error" => CrearErrorDecision(
                    informacionTecnica,
                    LeerString(raiz, "error")),
                _ => CrearErrorDecision(
                    informacionTecnica,
                    $"OpenCode devolvio una accion no soportada: {accion}.")
            };
        }
        catch (Exception excepcion)
            when (excepcion is JsonException or InvalidOperationException)
        {
            return CrearErrorDecision(
                informacionTecnica,
                excepcion.Message);
        }
    }

    private static ResultadoIntencionContexto CrearRespuesta(
        JsonElement raiz,
        string contenidoDecision,
        InformacionTecnicaLlamadaIAContexto informacionTecnica)
    {
        List<MensajeSalienteContexto> mensajes = LeerMensajesSalientes(raiz);
        if (mensajes.Count == 0)
        {
            return CrearErrorDecision(
                informacionTecnica,
                "La respuesta terminal no contiene mensajes salientes.");
        }

        return ResultadoIntencionContexto.Responder(
            informacionTecnica,
            contenidoDecision,
            mensajes.ToArray());
    }

    private static ResultadoIntencionContexto CrearComando(
        SolicitudIntencionContexto solicitud,
        JsonElement raiz,
        string contenidoDecision,
        string idMensajeAsistente,
        InformacionTecnicaLlamadaIAContexto informacionTecnica)
    {
        if (string.IsNullOrWhiteSpace(idMensajeAsistente))
        {
            return CrearErrorDecision(
                informacionTecnica,
                "OpenCode devolvio una decision de comando sin ID de mensaje assistant.");
        }

        string codigoComando = LeerString(raiz, "codigoComando");
        ComandoContexto? comando = solicitud.Comandos.SingleOrDefault(
            elemento => elemento.Autorizado
                && string.Equals(
                    elemento.Codigo,
                    codigoComando,
                    StringComparison.Ordinal));
        if (comando is null)
        {
            return CrearErrorDecision(
                informacionTecnica,
                $"OpenCode solicito un comando desconocido o no autorizado: {codigoComando}.");
        }

        Dictionary<string, string> parametros = LeerParametros(
            raiz,
            "parametros");
        List<string> faltantes = comando.Parametros.Keys
            .Where(parametro => !parametros.ContainsKey(parametro))
            .ToList();
        if (faltantes.Count > 0)
        {
            return CrearErrorDecision(
                informacionTecnica,
                $"OpenCode omitio parametros obligatorios de {codigoComando}: "
                + string.Join(", ", faltantes)
                + ".");
        }

        return ResultadoIntencionContexto.PedirComando(
            informacionTecnica,
            contenidoDecision,
            codigoComando,
            parametros,
            idMensajeAsistente);
    }

    private static ResultadoIntencionContexto CrearConsultaMensajesLineaAnterior(
        JsonElement raiz,
        string contenidoDecision,
        string idMensajeAsistente,
        InformacionTecnicaLlamadaIAContexto informacionTecnica)
    {
        if (string.IsNullOrWhiteSpace(idMensajeAsistente))
        {
            return CrearErrorDecision(
                informacionTecnica,
                "OpenCode devolvio una consulta anterior sin ID de mensaje assistant.");
        }

        int ciclosHaciaAtras = LeerEnteroPositivo(
            raiz,
            "ciclosHaciaAtras");
        return ResultadoIntencionContexto.ConsultarMensajesLineaAnterior(
            informacionTecnica,
            contenidoDecision,
            ciclosHaciaAtras,
            idMensajeAsistente);
    }

    private InformacionTecnicaLlamadaIAContexto CrearInformacionTecnicaLlamadaIA(
        int iteracion,
        string accion,
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado)
    {
        DTOOpenCodeRespuestaMensaje? respuesta = resultado.Respuesta;
        DTOOpenCodeMensajeAsistente? informacion = respuesta?.Informacion;
        DTOOpenCodeTokens? tokens = informacion?.Tokens;
        List<DTOOpenCodeParte> razonamientos = respuesta?.Partes
            .Where(parte => string.Equals(
                parte.Tipo,
                "reasoning",
                StringComparison.OrdinalIgnoreCase))
            .ToList()
            ?? [];

        return new InformacionTecnicaLlamadaIAContexto
        {
            Proveedor = string.IsNullOrWhiteSpace(
                informacion?.IDProveedor)
                ? "opencode"
                : informacion.IDProveedor,
            Modelo = string.IsNullOrWhiteSpace(
                informacion?.IDModelo)
                ? $"agente:{configuracion.NombreAgente}"
                : informacion.IDModelo,
            Adaptador = nameof(OpenCodeAgenteAdaptador),
            Iteracion = iteracion,
            AccionDecidida = accion,
            FinishReason = informacion?.RazonFinalizacion,
            NativeFinishReason = ObtenerFinalizacionNativa(
                respuesta?.Partes),
            PromptTokens = tokens?.Entrada,
            CompletionTokens = tokens?.Salida,
            ReasoningTokens = tokens?.Razonamiento,
            TotalTokens = tokens is null
                ? null
                : tokens.Entrada + tokens.Salida + tokens.Razonamiento,
            RequestJson = resultado.SolicitudJson,
            ResponseJson = resultado.RespuestaJson,
            Content = respuesta is null
                ? null
                : ObtenerContenido(respuesta.Partes),
            Reasoning = ObtenerRazonamiento(razonamientos),
            ReasoningDetailsJson = razonamientos.Count == 0
                ? null
                : JsonSerializer.Serialize(
                    razonamientos,
                    OpenCodeSerializacion.Opciones),
            Error = resultado.Error
        };
    }

    private static ResultadoIntencionContexto CrearErrorDecision(
        InformacionTecnicaLlamadaIAContexto informacionTecnica,
        string error)
    {
        informacionTecnica.Error = error;
        string contenido = JsonSerializer.Serialize(
            new
            {
                accion = "error",
                error
            });
        informacionTecnica.Content = contenido;
        return ResultadoIntencionContexto.ConError(
            informacionTecnica,
            contenido,
            error);
    }

    private static ResultadoCompactacionOpenCode CrearErrorCompactacion(
        InformacionTecnicaLlamadaIAContexto informacionTecnica,
        string error)
    {
        informacionTecnica.Error = error;
        return ResultadoCompactacionOpenCode.Fallo(
            error,
            informacionTecnica);
    }

    private static bool EsSalidaTruncada(
        DTOOpenCodeMensajeAsistente informacion)
    {
        return string.Equals(
                informacion.RazonFinalizacion,
                "length",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                informacion.Error?.Nombre,
                "MessageOutputLengthError",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsLimiteVentana(
        ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje> resultado)
    {
        if (string.Equals(
            resultado.TipoError,
            "context_length_exceeded",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (resultado.ErrorOpenCode is not null
            && EsLimiteVentana(resultado.ErrorOpenCode))
        {
            return true;
        }

        return ContieneValorExacto(
            resultado.RespuestaJson,
            "context_length_exceeded");
    }

    private static bool EsLimiteVentana(DTOOpenCodeError error)
    {
        if (string.Equals(
            error.Nombre,
            "context_length_exceeded",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContieneValorExacto(
            JsonSerializer.Serialize(
                error,
                OpenCodeSerializacion.Opciones),
            "context_length_exceeded");
    }

    private static bool ContieneValorExacto(
        string json,
        string valorEsperado)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            return ContieneValorExacto(
                documento.RootElement,
                valorEsperado);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContieneValorExacto(
        JsonElement elemento,
        string valorEsperado)
    {
        if (elemento.ValueKind == JsonValueKind.String)
        {
            return string.Equals(
                elemento.GetString(),
                valorEsperado,
                StringComparison.OrdinalIgnoreCase);
        }

        if (elemento.ValueKind == JsonValueKind.Object)
        {
            return elemento.EnumerateObject().Any(
                propiedad => ContieneValorExacto(
                    propiedad.Value,
                    valorEsperado));
        }

        if (elemento.ValueKind == JsonValueKind.Array)
        {
            return elemento.EnumerateArray().Any(
                valor => ContieneValorExacto(
                    valor,
                    valorEsperado));
        }

        return false;
    }

    private static string ObtenerContenido(
        IReadOnlyList<DTOOpenCodeParte> partes)
    {
        return string.Join(
            '\n',
            partes
                .Where(parte =>
                    string.Equals(
                        parte.Tipo,
                        "text",
                        StringComparison.OrdinalIgnoreCase)
                    && parte.Ignorada is not true
                    && !string.IsNullOrWhiteSpace(parte.Texto))
                .Select(parte => parte.Texto!.Trim()));
    }

    private static string? ObtenerRazonamiento(
        IReadOnlyList<DTOOpenCodeParte> partes)
    {
        List<string> textos = partes
            .Where(parte => !string.IsNullOrWhiteSpace(parte.Texto))
            .Select(parte => parte.Texto!.Trim())
            .ToList();
        return textos.Count == 0
            ? null
            : string.Join('\n', textos);
    }

    private static string? ObtenerFinalizacionNativa(
        IReadOnlyList<DTOOpenCodeParte>? partes)
    {
        return partes?
            .LastOrDefault(parte => string.Equals(
                parte.Tipo,
                "step-finish",
                StringComparison.OrdinalIgnoreCase))
            ?.Razon;
    }

    private static string? ObtenerMensajeError(
        DTOOpenCodeError error)
    {
        if (error.Datos.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (string propiedad in new[] { "message", "error", "reason" })
        {
            if (error.Datos.TryGetProperty(
                    propiedad,
                    out JsonElement valor)
                && valor.ValueKind == JsonValueKind.String)
            {
                return valor.GetString();
            }
        }

        return null;
    }

    private static string ObtenerNombreError(
        DTOOpenCodeError error,
        string valorPredeterminado)
    {
        return string.IsNullOrWhiteSpace(error.Nombre)
            ? valorPredeterminado
            : error.Nombre;
    }

    private static Dictionary<string, string> LeerParametros(
        JsonElement raiz,
        string propiedad)
    {
        if (!raiz.TryGetProperty(
                propiedad,
                out JsonElement parametrosJson)
            || parametrosJson.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"La respuesta no contiene el objeto requerido '{propiedad}'.");
        }

        Dictionary<string, string> parametros =
            new(StringComparer.Ordinal);
        foreach (JsonProperty parametro in parametrosJson.EnumerateObject())
        {
            parametros[parametro.Name] =
                parametro.Value.ValueKind == JsonValueKind.String
                    ? parametro.Value.GetString() ?? string.Empty
                    : parametro.Value.GetRawText();
        }

        return parametros;
    }

    private static int LeerEnteroPositivo(
        JsonElement raiz,
        string propiedad)
    {
        if (!raiz.TryGetProperty(propiedad, out JsonElement valor)
            || valor.ValueKind != JsonValueKind.Number
            || !valor.TryGetInt32(out int resultado)
            || resultado <= 0)
        {
            throw new InvalidOperationException(
                $"La respuesta debe indicar la propiedad entera positiva '{propiedad}'.");
        }

        return resultado;
    }

    private static List<MensajeSalienteContexto> LeerMensajesSalientes(
        JsonElement raiz)
    {
        List<MensajeSalienteContexto> mensajes = [];
        if (raiz.TryGetProperty(
                "mensajes",
                out JsonElement mensajesJson)
            && mensajesJson.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement mensajeJson in mensajesJson.EnumerateArray())
            {
                mensajes.Add(new MensajeSalienteContexto
                {
                    TipoMensaje =
                        LeerStringOpcional(
                            mensajeJson,
                            "tipoMensaje")
                        ?? "texto",
                    Contenido = LeerString(
                        mensajeJson,
                        "contenido"),
                    FechaMensaje = DateTime.Now
                });
            }

            return mensajes;
        }

        string? contenido = LeerStringOpcional(
            raiz,
            "contenido");
        if (!string.IsNullOrWhiteSpace(contenido))
        {
            mensajes.Add(new MensajeSalienteContexto
            {
                TipoMensaje = "texto",
                Contenido = contenido,
                FechaMensaje = DateTime.Now
            });
        }

        return mensajes;
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
        const string prefijo = "[fecha_creacion=";
        if (!contenido.StartsWith(
            prefijo,
            StringComparison.Ordinal))
        {
            return contenido;
        }

        int cierre = contenido.IndexOf(']');
        if (cierre < prefijo.Length)
        {
            return contenido;
        }

        string fecha = contenido[prefijo.Length..cierre];
        if (!DateTimeOffset.TryParse(
            fecha,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            return contenido;
        }

        return contenido[(cierre + 1)..].TrimStart();
    }

    private static string LeerString(
        JsonElement raiz,
        string propiedad)
    {
        string? valor = LeerStringOpcional(raiz, propiedad);
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(
                $"La respuesta no contiene la propiedad string requerida '{propiedad}'.");
        }

        return valor;
    }

    private static string? LeerStringOpcional(
        JsonElement raiz,
        string propiedad)
    {
        if (raiz.TryGetProperty(
                propiedad,
                out JsonElement valor)
            && valor.ValueKind == JsonValueKind.String)
        {
            return valor.GetString();
        }

        return null;
    }
}
