using System.Text.Json;
using PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;
using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class ContextoConversacionServicio : IContextoConversacionServicio
{
    private const string RolUsuario = "user";
    private const string RolAsistente = "assistant";
    private const string RolHerramienta = "tool";
    private const string TipoEntradaMensajeEntrada = "mensaje_entrada";
    private const string TipoMetadataEntradaDecisionComando = "decision_comando";
    private const string TipoMetadataEntradaDecisionConsultaMensajesLineaAnterior = "decision_consulta_mensajes_linea_anterior";
    private const string TipoEntradaRespuestaFinal = "respuesta_final";
    private const string TipoEntradaNoResponder = "no_responder";
    private const string TipoEntradaErrorIntencion = "error_intencion";
    private const string TipoMetadataEntradaResultadoComando = "resultado_comando";
    private const string TipoMetadataEntradaResultadoConsultaMensajesLineaAnterior = "resultado_consulta_mensajes_linea_anterior";
    private const string TipoEntradaLimiteVentana = "limite_ventana";

    private readonly IReadOnlyList<IFiltroContextoConversacion> filtros;
    private readonly IIntencionContextoConversacionServicio intencionContextoConversacionServicio;
    private readonly IProveedorCatalogoComandoContextoServicio proveedorCatalogoComandoContextoServicio;
    private readonly IEjecucionComandoContextoAplicacion ejecucionComandoContextoAplicacion;
    private readonly IConsultaMensajesLineaConversacionAnteriorAplicacion consultaMensajesLineaConversacionAnteriorAplicacion;
    private readonly IRegistrarContextoIAAplicacion registrarContextoIAAplicacion;
    private readonly ICompactacionContextoConversacionAplicacion compactacionContextoConversacionAplicacion;
    private readonly ConfiguracionContextoConversacion configuracion;

    public ContextoConversacionServicio(
        IEnumerable<IFiltroContextoConversacion> filtros,
        IIntencionContextoConversacionServicio intencionContextoConversacionServicio,
        IProveedorCatalogoComandoContextoServicio proveedorCatalogoComandoContextoServicio,
        IEjecucionComandoContextoAplicacion ejecucionComandoContextoAplicacion,
        IConsultaMensajesLineaConversacionAnteriorAplicacion consultaMensajesLineaConversacionAnteriorAplicacion,
        IRegistrarContextoIAAplicacion registrarContextoIAAplicacion,
        ICompactacionContextoConversacionAplicacion compactacionContextoConversacionAplicacion,
        ConfiguracionContextoConversacion configuracion)
    {
        this.filtros = filtros.ToList();
        this.intencionContextoConversacionServicio = intencionContextoConversacionServicio;
        this.proveedorCatalogoComandoContextoServicio = proveedorCatalogoComandoContextoServicio;
        this.ejecucionComandoContextoAplicacion = ejecucionComandoContextoAplicacion;
        this.consultaMensajesLineaConversacionAnteriorAplicacion = consultaMensajesLineaConversacionAnteriorAplicacion;
        this.registrarContextoIAAplicacion = registrarContextoIAAplicacion;
        this.compactacionContextoConversacionAplicacion = compactacionContextoConversacionAplicacion;
        this.configuracion = configuracion;
    }

    public async Task<ResultadoContextoConversacion> ResolverAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ComandoContexto> comandos = await proveedorCatalogoComandoContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);
        CompactacionContextoConversacion? compactacionContextoInicial = await compactacionContextoConversacionAplicacion.ObtenerInicialAsync(
            solicitud.IDLineaConversacion,
            cancellationToken);
        List<MetadataEntradaContextoIA> metadataEntradasContextoIA = (await registrarContextoIAAplicacion.ObtenerMetadataEntradasAsync(
            solicitud.IDLineaConversacion,
            cancellationToken)).ToList();

        await AsegurarMetadataEntradasMensajesInicialesAsync(
            solicitud,
            metadataEntradasContextoIA,
            cancellationToken);

        ResultadoEjecucionComandoContexto? ejecucionRecuperada = await ejecucionComandoContextoAplicacion.ReanudarActivaAsync(
            solicitud,
            comandos,
            cancellationToken);
        if (ejecucionRecuperada is not null)
        {
            if (ejecucionRecuperada.MetadataEntradaResultado is not null)
            {
                metadataEntradasContextoIA.Add(ejecucionRecuperada.MetadataEntradaResultado);
            }

            if (!ejecucionRecuperada.Resultado.Exitoso)
            {
                return CrearError(
                    ejecucionRecuperada.Resultado.Error ?? "Fallo la recuperacion de la ejecucion del comando.");
            }
        }

        List<MetadataEntradaContextoIA> entradasProcesamiento = metadataEntradasContextoIA
            .Where(entrada => entrada.IDProcesamientoInternoMensaje == solicitud.IDProcesamientoInternoMensaje)
            .ToList();

        List<DatoIntermedioContexto> datosIntermedios = CrearDatosIntermedios(entradasProcesamiento);
        int iteracionInicial = entradasProcesamiento.Count(entrada => entrada.IDInformacionTecnicaLlamadaIA.HasValue) + 1;

        for (int iteracion = iteracionInicial; iteracion <= configuracion.MaximoIteraciones; iteracion++)
        {
            List<MetadataEntradaContextoIA> metadataEntradasSolicitudIA = await ConstruirMetadataEntradasSolicitudIAAsync(
                solicitud,
                metadataEntradasContextoIA,
                cancellationToken);

            EstadoIteracionContextoConversacion estadoIteracion = new()
            {
                Solicitud = solicitud,
                Comandos = comandos,
                DatosIntermedios = datosIntermedios,
                Iteracion = iteracion
            };

            ResultadoPasoContexto resultadoFiltros = await EjecutarFiltrosAsync(estadoIteracion, cancellationToken);
            if (resultadoFiltros.Tipo == ResultadoPasoContextoTipo.Terminar)
                return ObtenerResultadoFinal(resultadoFiltros);

            ResultadoIntencionContexto decision = await intencionContextoConversacionServicio.DecidirAsync(
                new SolicitudIntencionContexto
                {
                    Solicitud = solicitud,
                    Comandos = comandos,
                    DatosIntermedios = datosIntermedios,
                    MetadataEntradasContextoIA = metadataEntradasSolicitudIA,
                    CompactacionContextoInicial = compactacionContextoInicial,
                    Iteracion = iteracion
                },
                cancellationToken);

            ValidarContratoInformacionTecnicaLlamadaIA(decision, iteracion);
            ComandoContexto? comandoDecision = ObtenerComandoAutorizado(decision, comandos);
            SolicitudPrepararEjecucionComandoContexto? preparacionEjecucion = CrearPreparacionEjecucion(
                decision,
                comandoDecision);
            ResultadoRegistrarDecisionContextoIA registroDecision = await registrarContextoIAAplicacion.RegistrarDecisionAsync(
                solicitud,
                decision.InformacionTecnicaLlamadaIA,
                CrearSolicitudMetadataEntradaDecision(solicitud, decision),
                preparacionEjecucion,
                cancellationToken);
            metadataEntradasContextoIA.Add(registroDecision.MetadataEntradaDecision);

            ResultadoPasoContexto resultadoDecision = await ProcesarDecisionAsync(
                solicitud,
                comandos,
                datosIntermedios,
                metadataEntradasContextoIA,
                metadataEntradasSolicitudIA,
                compactacionContextoInicial,
                decision,
                registroDecision.EjecucionComando,
                iteracion,
                cancellationToken);

            if (resultadoDecision.Tipo == ResultadoPasoContextoTipo.Continuar)
            {
                continue;
            }

            return ObtenerResultadoFinal(resultadoDecision);
        }

        return CrearError("Se alcanzo el maximo de iteraciones del contexto.");
    }

    private async Task AsegurarMetadataEntradasMensajesInicialesAsync(
        SolicitudContextoConversacion solicitud,
        List<MetadataEntradaContextoIA> metadataEntradasContextoIA,
        CancellationToken cancellationToken)
    {
        foreach (MensajeEntranteContexto mensaje in ObtenerMensajesEntrantes(solicitud))
        {
            bool yaExiste = metadataEntradasContextoIA.Any(entrada =>
                entrada.IDTipoEntradaContextoIA == TipoEntradaMensajeEntrada
                && entrada.IDMensaje == mensaje.IDMensaje);
            if (yaExiste)
            {
                continue;
            }

            MetadataEntradaContextoIA entrada = await registrarContextoIAAplicacion.RegistrarMetadataEntradaAsync(
                new SolicitudRegistrarMetadataEntradaContextoIA
                {
                    IDLineaConversacion = solicitud.IDLineaConversacion,
                    IDMensaje = mensaje.IDMensaje,
                    IDProcesamientoInternoMensaje = mensaje.IDProcesamientoInternoMensaje,
                    IDRolContextoIA = RolUsuario,
                    IDTipoEntradaContextoIA = TipoEntradaMensajeEntrada,
                    Contenido = mensaje.Contenido,
                    FechaEntrada = mensaje.FechaMensaje
                },
                cancellationToken);

            metadataEntradasContextoIA.Add(entrada);
        }
    }

    private async Task<ResultadoPasoContexto> EjecutarFiltrosAsync(
        EstadoIteracionContextoConversacion estado,
        CancellationToken cancellationToken)
    {
        foreach (IFiltroContextoConversacion filtro in filtros)
        {
            ResultadoFiltroContexto resultadoFiltro = await filtro.EjecutarAsync(estado, cancellationToken);
            if (!resultadoFiltro.Continuar)
            {
                return ResultadoPasoContexto.Terminar(
                    CrearError(resultadoFiltro.Error ?? "Un filtro detuvo el contexto."));
            }
        }

        return ResultadoPasoContexto.Continuar();
    }

    private async Task<ResultadoPasoContexto> ProcesarDecisionAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        List<DatoIntermedioContexto> datosIntermedios,
        List<MetadataEntradaContextoIA> metadataEntradasContextoIA,
        IReadOnlyList<MetadataEntradaContextoIA> metadataEntradasSolicitudIA,
        CompactacionContextoConversacion? compactacionContextoInicial,
        ResultadoIntencionContexto decision,
        EjecucionComandoContexto? ejecucionComando,
        int iteracion,
        CancellationToken cancellationToken)
    {
        if (decision.TipoAccion == AccionContextoTipo.Responder)
        {
            return ResultadoPasoContexto.Terminar(new ResultadoContextoConversacion
            {
                TipoResultado = ResultadoContextoConversacionTipo.ConSalidas,
                MensajesSalientes = decision.MensajesSalientes
            });
        }

        if (decision.TipoAccion == AccionContextoTipo.NoResponder)
        {
            return ResultadoPasoContexto.Terminar(new ResultadoContextoConversacion
            {
                TipoResultado = ResultadoContextoConversacionTipo.SinSalidas
            });
        }

        if (decision.TipoAccion == AccionContextoTipo.Error)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError(decision.Error ?? "La IA de intencion devolvio error."));
        }

        if (decision.TipoAccion == AccionContextoTipo.Comando)
        {
            return await ProcesarComandoAsync(
                solicitud,
                comandos,
                datosIntermedios,
                metadataEntradasContextoIA,
                decision,
                ejecucionComando,
                cancellationToken);
        }

        if (decision.TipoAccion == AccionContextoTipo.ConsultarMensajesLineaAnterior)
        {
            return await ProcesarConsultaMensajesLineaAnteriorAsync(
                solicitud,
                metadataEntradasContextoIA,
                decision,
                cancellationToken);
        }

        if (decision.TipoAccion == AccionContextoTipo.LimiteVentanaAlcanzado)
        {
            return await ProcesarLimiteVentanaAsync(
                solicitud,
                metadataEntradasSolicitudIA,
                compactacionContextoInicial,
                iteracion,
                cancellationToken);
        }

        return ResultadoPasoContexto.Terminar(CrearError("Accion de contexto no soportada."));
    }

    private async Task<ResultadoPasoContexto> ProcesarComandoAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        List<DatoIntermedioContexto> datosIntermedios,
        List<MetadataEntradaContextoIA> metadataEntradasContextoIA,
        ResultadoIntencionContexto decision,
        EjecucionComandoContexto? ejecucion,
        CancellationToken cancellationToken)
    {
        ComandoContexto? comando = comandos.SingleOrDefault(
            comandoActual => comandoActual.Codigo == decision.CodigoComando && comandoActual.Autorizado);
        if (comando is null)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError($"Comando no autorizado: {decision.CodigoComando}"));
        }

        if (ejecucion is null)
        {
            throw new InvalidOperationException(
                "Una decision de comando autorizada debe registrar su ejecucion durable en la misma transaccion.");
        }

        ResultadoEjecucionComandoContexto resultadoEjecucion = await ejecucionComandoContextoAplicacion.EjecutarAsync(
            solicitud,
            ejecucion,
            comando,
            decision.ParametrosComando,
            cancellationToken);
        ResultadoComandoContexto resultadoComando = resultadoEjecucion.Resultado;

        if (resultadoEjecucion.MetadataEntradaResultado is not null)
        {
            metadataEntradasContextoIA.Add(resultadoEjecucion.MetadataEntradaResultado);
        }

        if (!resultadoComando.Exitoso)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError(resultadoComando.Error ?? "Fallo la ejecucion del comando."));
        }

        datosIntermedios.Add(new DatoIntermedioContexto
        {
            Tipo = "comando",
            Contenido = resultadoComando.Resultado
        });

        return ResultadoPasoContexto.Continuar();
    }

    private async Task<ResultadoPasoContexto> ProcesarConsultaMensajesLineaAnteriorAsync(
        SolicitudContextoConversacion solicitud,
        List<MetadataEntradaContextoIA> metadataEntradasContextoIA,
        ResultadoIntencionContexto decision,
        CancellationToken cancellationToken)
    {
        if (!decision.CiclosHaciaAtras.HasValue || decision.CiclosHaciaAtras.Value <= 0)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError("La consulta de mensajes anteriores debe indicar ciclosHaciaAtras mayor que cero."));
        }

        IReadOnlyList<MetadataEntradaContextoIA> ciclo = await consultaMensajesLineaConversacionAnteriorAplicacion.ObtenerCicloAsync(
            solicitud.IDConversacion,
            solicitud.IDLineaConversacion,
            decision.CiclosHaciaAtras.Value,
            cancellationToken);
        string estado = "sin_resultados";
        long? idLineaConversacionOrigen = null;
        long? idProcesamientoInternoMensajeOrigen = null;

        if (ciclo.Count > 0)
        {
            MetadataEntradaContextoIA primeraEntrada = ciclo[0];
            idLineaConversacionOrigen = primeraEntrada.IDLineaConversacion;
            idProcesamientoInternoMensajeOrigen = primeraEntrada.IDProcesamientoInternoMensaje
                ?? throw new InvalidOperationException("El ciclo anterior no tiene procesamiento asociado.");
            bool yaCargado = metadataEntradasContextoIA.Any(entrada =>
                entrada.IDTipoEntradaContextoIA == TipoMetadataEntradaResultadoConsultaMensajesLineaAnterior
                && ReferenciaConsultaCoincide(entrada.Contenido, idProcesamientoInternoMensajeOrigen.Value));
            estado = yaCargado ? "ya_cargada" : "cargada";
        }

        string contenidoResultado = JsonSerializer.Serialize(new
        {
            ciclosHaciaAtras = decision.CiclosHaciaAtras.Value,
            idLineaConversacion = idLineaConversacionOrigen,
            idProcesamientoInternoMensaje = idProcesamientoInternoMensajeOrigen,
            cantidadEntradas = ciclo.Count,
            estado
        });

        MetadataEntradaContextoIA entrada = await registrarContextoIAAplicacion.RegistrarMetadataEntradaAsync(
            new SolicitudRegistrarMetadataEntradaContextoIA
            {
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDRolContextoIA = RolHerramienta,
                IDTipoEntradaContextoIA = TipoMetadataEntradaResultadoConsultaMensajesLineaAnterior,
                Contenido = contenidoResultado,
                ToolCallID = decision.ToolCallID,
                FechaEntrada = DateTime.Now
            },
            cancellationToken);

        metadataEntradasContextoIA.Add(entrada);
        return ResultadoPasoContexto.Continuar();
    }

    private async Task<ResultadoPasoContexto> ProcesarLimiteVentanaAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<MetadataEntradaContextoIA> metadataEntradasContextoIA,
        CompactacionContextoConversacion? compactacionContextoInicial,
        int iteracion,
        CancellationToken cancellationToken)
    {
        HashSet<long> idsProcesamientosActuales = ObtenerIDsProcesamientosActuales(solicitud);
        List<MetadataEntradaContextoIA> entradasCompactables = metadataEntradasContextoIA
            .Where(entrada =>
                !entrada.IDProcesamientoInternoMensaje.HasValue
                || !idsProcesamientosActuales.Contains(entrada.IDProcesamientoInternoMensaje.Value))
            .ToList();

        if (compactacionContextoInicial is null && entradasCompactables.Count == 0)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError("No existe contexto anterior reducible para renovar la linea."));
        }

        ResultadoCompactacionIntencionContexto compactacion = await intencionContextoConversacionServicio.CompactarAsync(
            new SolicitudCompactacionIntencionContexto
            {
                Solicitud = solicitud,
                CompactacionContextoInicial = compactacionContextoInicial,
                MetadataEntradasContextoIA = entradasCompactables,
                Iteracion = iteracion
            },
            cancellationToken);

        foreach (InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA in compactacion.InformacionesTecnicasLlamadasIA)
        {
            ValidarInformacionTecnicaLlamadaIA(informacionTecnicaLlamadaIA, iteracion, "Compactar");
        }
        if (!compactacion.Exitoso)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError(compactacion.Error ?? "No se pudo compactar el contexto anterior."));
        }

        if (string.IsNullOrWhiteSpace(compactacion.Contenido))
        {
            return ResultadoPasoContexto.Terminar(
                CrearError("La compactacion no produjo contenido para la nueva compactacion."));
        }

        return ResultadoPasoContexto.Terminar(new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado,
            Compactacion = compactacion
        });
    }

    private static SolicitudRegistrarMetadataEntradaContextoIA CrearSolicitudMetadataEntradaDecision(
        SolicitudContextoConversacion solicitud,
        ResultadoIntencionContexto decision)
    {
        return new SolicitudRegistrarMetadataEntradaContextoIA
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDMensaje = solicitud.IDMensaje,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDRolContextoIA = RolAsistente,
            IDTipoEntradaContextoIA = ObtenerTipoMetadataEntradaDecision(decision.TipoAccion),
            Contenido = decision.ContenidoDecision,
            ToolCallID = decision.ToolCallID,
            FechaEntrada = DateTime.Now
        };
    }

    private static ComandoContexto? ObtenerComandoAutorizado(
        ResultadoIntencionContexto decision,
        IReadOnlyList<ComandoContexto> comandos)
    {
        if (decision.TipoAccion != AccionContextoTipo.Comando)
        {
            return null;
        }

        return comandos.SingleOrDefault(comando =>
            comando.Codigo == decision.CodigoComando && comando.Autorizado);
    }

    private SolicitudPrepararEjecucionComandoContexto? CrearPreparacionEjecucion(
        ResultadoIntencionContexto decision,
        ComandoContexto? comando)
    {
        if (decision.TipoAccion != AccionContextoTipo.Comando || comando is null)
        {
            return null;
        }

        return new SolicitudPrepararEjecucionComandoContexto
        {
            ProveedorEjecucion = ejecucionComandoContextoAplicacion.Proveedor,
            CodigoComando = comando.Codigo,
            ParametrosJson = JsonSerializer.Serialize(decision.ParametrosComando)
        };
    }

    private static void ValidarContratoInformacionTecnicaLlamadaIA(
        ResultadoIntencionContexto decision,
        int iteracion)
    {
        if (decision.InformacionTecnicaLlamadaIA is null)
            throw new InvalidOperationException("La intencion de contexto debe retornar informacion tecnica de la llamada IA obligatoria.");

        if (string.IsNullOrWhiteSpace(decision.ContenidoDecision))
            throw new InvalidOperationException("La intencion de contexto debe retornar contenido de decision obligatorio.");

        ValidarInformacionTecnicaLlamadaIA(decision.InformacionTecnicaLlamadaIA, iteracion, decision.TipoAccion.ToString());
    }

    private static void ValidarInformacionTecnicaLlamadaIA(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        int iteracion,
        string accion)
    {
        if (informacionTecnicaLlamadaIA is null)
            throw new InvalidOperationException("La intencion de contexto debe retornar informacion tecnica de la llamada IA obligatoria.");

        if (string.IsNullOrWhiteSpace(informacionTecnicaLlamadaIA.Proveedor))
            throw new InvalidOperationException("La informacion tecnica de la llamada IA debe indicar el proveedor.");

        if (string.IsNullOrWhiteSpace(informacionTecnicaLlamadaIA.Modelo))
            throw new InvalidOperationException("La informacion tecnica de la llamada IA debe indicar el modelo.");

        if (string.IsNullOrWhiteSpace(informacionTecnicaLlamadaIA.Adaptador))
            throw new InvalidOperationException("La informacion tecnica de la llamada IA debe indicar el adaptador.");

        informacionTecnicaLlamadaIA.Iteracion = iteracion;
        informacionTecnicaLlamadaIA.AccionDecidida = accion;
    }

    private static string ObtenerTipoMetadataEntradaDecision(AccionContextoTipo tipoAccion)
    {
        return tipoAccion switch
        {
            AccionContextoTipo.Comando => TipoMetadataEntradaDecisionComando,
            AccionContextoTipo.ConsultarMensajesLineaAnterior => TipoMetadataEntradaDecisionConsultaMensajesLineaAnterior,
            AccionContextoTipo.Responder => TipoEntradaRespuestaFinal,
            AccionContextoTipo.NoResponder => TipoEntradaNoResponder,
            AccionContextoTipo.Error => TipoEntradaErrorIntencion,
            AccionContextoTipo.LimiteVentanaAlcanzado => TipoEntradaLimiteVentana,
            _ => TipoEntradaErrorIntencion
        };
    }

    private static List<DatoIntermedioContexto> CrearDatosIntermedios(
        IReadOnlyList<MetadataEntradaContextoIA> metadataEntradasContextoIA)
    {
        return metadataEntradasContextoIA
            .Where(entrada => entrada.IDTipoEntradaContextoIA == TipoMetadataEntradaResultadoComando)
            .Select(entrada => new DatoIntermedioContexto
            {
                Tipo = "comando",
                Contenido = entrada.Contenido
            })
            .ToList();
    }

    private async Task<List<MetadataEntradaContextoIA>> ConstruirMetadataEntradasSolicitudIAAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<MetadataEntradaContextoIA> metadataEntradasLineaActual,
        CancellationToken cancellationToken)
    {
        List<MetadataEntradaContextoIA> resultado = [];
        foreach (MetadataEntradaContextoIA entrada in metadataEntradasLineaActual.OrderBy(entrada => entrada.Orden).ThenBy(entrada => entrada.ID))
        {
            resultado.Add(entrada);
            if (entrada.IDTipoEntradaContextoIA != TipoMetadataEntradaResultadoConsultaMensajesLineaAnterior
                || entrada.IDCompactacionContextoIncorporada.HasValue)
            {
                continue;
            }

            if (!TryLeerEstadoConsulta(entrada.Contenido, out string estado))
            {
                throw new InvalidOperationException(
                    $"La metadata-entrada {entrada.ID} contiene un resultado de consulta de mensajes anteriores invalido.");
            }

            if (estado != "cargada")
            {
                continue;
            }

            if (!TryLeerReferenciaConsulta(
                    entrada.Contenido,
                    out long idLineaOrigen,
                    out long idProcesamientoOrigen,
                    out _))
            {
                throw new InvalidOperationException(
                    $"La metadata-entrada {entrada.ID} no identifica el ciclo anterior que declaro como cargado.");
            }

            IReadOnlyList<MetadataEntradaContextoIA> ciclo = await consultaMensajesLineaConversacionAnteriorAplicacion.ObtenerCicloReferenciadoAsync(
                solicitud.IDConversacion,
                solicitud.IDLineaConversacion,
                idLineaOrigen,
                idProcesamientoOrigen,
                cancellationToken);
            resultado.AddRange(ciclo);
        }

        return resultado;
    }

    private static bool TryLeerEstadoConsulta(string? contenido, out string estado)
    {
        estado = string.Empty;
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return false;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(contenido);
            if (!documento.RootElement.TryGetProperty("estado", out JsonElement estadoJson)
                || estadoJson.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            estado = estadoJson.GetString() ?? string.Empty;
            return estado is "cargada" or "ya_cargada" or "sin_resultados";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ReferenciaConsultaCoincide(string? contenido, long idProcesamientoInternoMensaje)
    {
        return TryLeerReferenciaConsulta(contenido, out _, out long idProcesamientoOrigen, out string estado)
            && estado == "cargada"
            && idProcesamientoOrigen == idProcesamientoInternoMensaje;
    }

    private static bool TryLeerReferenciaConsulta(
        string? contenido,
        out long idLineaConversacion,
        out long idProcesamientoInternoMensaje,
        out string estado)
    {
        idLineaConversacion = 0;
        idProcesamientoInternoMensaje = 0;
        estado = string.Empty;
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return false;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(contenido);
            JsonElement raiz = documento.RootElement;
            if (!raiz.TryGetProperty("idLineaConversacion", out JsonElement linea)
                || linea.ValueKind != JsonValueKind.Number
                || !linea.TryGetInt64(out idLineaConversacion)
                || !raiz.TryGetProperty("idProcesamientoInternoMensaje", out JsonElement procesamiento)
                || procesamiento.ValueKind != JsonValueKind.Number
                || !procesamiento.TryGetInt64(out idProcesamientoInternoMensaje)
                || !raiz.TryGetProperty("estado", out JsonElement estadoJson))
            {
                return false;
            }

            estado = estadoJson.GetString() ?? string.Empty;
            return idLineaConversacion > 0 && idProcesamientoInternoMensaje > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ResultadoContextoConversacion ObtenerResultadoFinal(ResultadoPasoContexto resultadoPaso)
    {
        return resultadoPaso.ResultadoFinal
            ?? throw new InvalidOperationException("Un paso terminal debe contener un resultado final.");
    }

    private static ResultadoContextoConversacion CrearError(string error)
    {
        return new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.Error,
            Error = error
        };
    }

    private static IReadOnlyList<MensajeEntranteContexto> ObtenerMensajesEntrantes(
        SolicitudContextoConversacion solicitud)
    {
        if (solicitud.MensajesEntrantes.Count > 0)
        {
            return solicitud.MensajesEntrantes
                .OrderBy(mensaje => mensaje.FechaMensaje)
                .ThenBy(mensaje => mensaje.IDMensaje)
                .ToList();
        }

        return
        [
            new MensajeEntranteContexto
            {
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDMensaje = solicitud.IDMensaje,
                TipoMensaje = solicitud.TipoMensaje,
                TelefonoOrigen = solicitud.TelefonoOrigen,
                TelefonoDestino = solicitud.TelefonoDestino,
                Contenido = solicitud.Contenido,
                IdentificadorExternoMensaje = solicitud.IdentificadorExternoMensaje,
                FechaMensaje = solicitud.FechaMensaje,
                Archivos = solicitud.Archivos
            }
        ];
    }

    private static HashSet<long> ObtenerIDsProcesamientosActuales(
        SolicitudContextoConversacion solicitud)
    {
        if (solicitud.IDsProcesamientosInternosMensaje.Count > 0)
        {
            return solicitud.IDsProcesamientosInternosMensaje.ToHashSet();
        }

        return [solicitud.IDProcesamientoInternoMensaje];
    }
}
