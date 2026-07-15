using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class ContextoConversacionServicio : IContextoConversacionServicio
{
    private const string RolUsuario = "user";
    private const string RolAsistente = "assistant";
    private const string RolHerramienta = "tool";
    private const string TipoEntradaMensajeEntrada = "mensaje_entrada";
    private const string TipoEntradaDecisionComando = "decision_comando";
    private const string TipoEntradaDecisionHistorial = "decision_historial";
    private const string TipoEntradaRespuestaFinal = "respuesta_final";
    private const string TipoEntradaNoResponder = "no_responder";
    private const string TipoEntradaErrorIntencion = "error_intencion";
    private const string TipoEntradaResultadoComando = "resultado_comando";
    private const string TipoEntradaResultadoHistorial = "resultado_historial";
    private const string TipoEntradaLimiteVentana = "limite_ventana";

    private readonly IReadOnlyList<IFiltroContextoConversacion> filtros;
    private readonly IIntencionContextoConversacionServicio intencionContextoConversacionServicio;
    private readonly IProveedorCatalogoComandoContextoServicio proveedorCatalogoComandoContextoServicio;
    private readonly IEjecutorComandoContextoServicio ejecutorComandoContextoServicio;
    private readonly IProveedorHistorialContextoServicio proveedorHistorialContextoServicio;
    private readonly IRegistrarContextoIAAplicacion registrarContextoIAAplicacion;
    private readonly IEstadoContextoConversacionAplicacion estadoContextoConversacionAplicacion;
    private readonly ConfiguracionContextoConversacion configuracion;

    public ContextoConversacionServicio(
        IEnumerable<IFiltroContextoConversacion> filtros,
        IIntencionContextoConversacionServicio intencionContextoConversacionServicio,
        IProveedorCatalogoComandoContextoServicio proveedorCatalogoComandoContextoServicio,
        IEjecutorComandoContextoServicio ejecutorComandoContextoServicio,
        IProveedorHistorialContextoServicio proveedorHistorialContextoServicio,
        IRegistrarContextoIAAplicacion registrarContextoIAAplicacion,
        IEstadoContextoConversacionAplicacion estadoContextoConversacionAplicacion,
        ConfiguracionContextoConversacion configuracion)
    {
        this.filtros = filtros.ToList();
        this.intencionContextoConversacionServicio = intencionContextoConversacionServicio;
        this.proveedorCatalogoComandoContextoServicio = proveedorCatalogoComandoContextoServicio;
        this.ejecutorComandoContextoServicio = ejecutorComandoContextoServicio;
        this.proveedorHistorialContextoServicio = proveedorHistorialContextoServicio;
        this.registrarContextoIAAplicacion = registrarContextoIAAplicacion;
        this.estadoContextoConversacionAplicacion = estadoContextoConversacionAplicacion;
        this.configuracion = configuracion;
    }

    public async Task<ResultadoContextoConversacion> ResolverAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ComandoContexto> comandos = await proveedorCatalogoComandoContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);
        EstadoContextoConversacion? estadoContextoInicial = await estadoContextoConversacionAplicacion.ObtenerInicialAsync(
            solicitud.IDLineaConversacion,
            cancellationToken);
        List<EntradaContextoIA> entradasContextoIA = (await registrarContextoIAAplicacion.ObtenerEntradasAsync(
            solicitud.IDLineaConversacion,
            cancellationToken)).ToList();

        await AsegurarEntradaMensajeInicialAsync(solicitud, entradasContextoIA, cancellationToken);

        List<EntradaContextoIA> entradasProcesamiento = entradasContextoIA
            .Where(entrada => entrada.IDProcesamientoInternoMensaje == solicitud.IDProcesamientoInternoMensaje)
            .ToList();

        List<DatoIntermedioContexto> datosIntermedios = CrearDatosIntermedios(entradasProcesamiento);
        int iteracionInicial = entradasProcesamiento.Count(entrada => entrada.IDMetadataRazonamientoIA.HasValue) + 1;

        for (int iteracion = iteracionInicial; iteracion <= configuracion.MaximoIteraciones; iteracion++)
        {
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
                    EntradasContextoIA = entradasContextoIA,
                    EstadoContextoInicial = estadoContextoInicial,
                    Iteracion = iteracion
                },
                cancellationToken);

            ValidarContratoMetadata(decision, iteracion);
            EntradaContextoIA entradaDecision = await registrarContextoIAAplicacion.RegistrarDecisionAsync(
                solicitud,
                decision.Metadata,
                CrearSolicitudEntradaDecision(solicitud, decision),
                cancellationToken);
            entradasContextoIA.Add(entradaDecision);

            ResultadoPasoContexto resultadoDecision = await ProcesarDecisionAsync(
                solicitud,
                comandos,
                datosIntermedios,
                entradasContextoIA,
                estadoContextoInicial,
                decision,
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

    private async Task AsegurarEntradaMensajeInicialAsync(
        SolicitudContextoConversacion solicitud,
        List<EntradaContextoIA> entradasContextoIA,
        CancellationToken cancellationToken)
    {
        bool yaExiste = entradasContextoIA.Any(entrada =>
            entrada.IDTipoEntradaContextoIA == TipoEntradaMensajeEntrada
            && entrada.IDMensaje == solicitud.IDMensaje);
        if (yaExiste)
            return;

        EntradaContextoIA entrada = await registrarContextoIAAplicacion.RegistrarEntradaAsync(
            new SolicitudRegistrarEntradaContextoIA
            {
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDRolContextoIA = RolUsuario,
                IDTipoEntradaContextoIA = TipoEntradaMensajeEntrada,
                Contenido = solicitud.Contenido,
                FechaEntrada = solicitud.FechaMensaje
            },
            cancellationToken);

        entradasContextoIA.Add(entrada);
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
        List<EntradaContextoIA> entradasContextoIA,
        EstadoContextoConversacion? estadoContextoInicial,
        ResultadoIntencionContexto decision,
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
                entradasContextoIA,
                decision,
                cancellationToken);
        }

        if (decision.TipoAccion == AccionContextoTipo.Historial)
        {
            return await ProcesarHistorialAsync(
                solicitud,
                datosIntermedios,
                entradasContextoIA,
                cancellationToken);
        }

        if (decision.TipoAccion == AccionContextoTipo.LimiteVentanaAlcanzado)
        {
            return await ProcesarLimiteVentanaAsync(
                solicitud,
                entradasContextoIA,
                estadoContextoInicial,
                iteracion,
                cancellationToken);
        }

        return ResultadoPasoContexto.Terminar(CrearError("Accion de contexto no soportada."));
    }

    private async Task<ResultadoPasoContexto> ProcesarComandoAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        List<DatoIntermedioContexto> datosIntermedios,
        List<EntradaContextoIA> entradasContextoIA,
        ResultadoIntencionContexto decision,
        CancellationToken cancellationToken)
    {
        ComandoContexto? comando = comandos.SingleOrDefault(
            comandoActual => comandoActual.Codigo == decision.CodigoComando && comandoActual.Autorizado);
        if (comando is null)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError($"Comando no autorizado: {decision.CodigoComando}"));
        }

        ResultadoComandoContexto resultadoComando = await ejecutorComandoContextoServicio.EjecutarAsync(
            new SolicitudEjecutarComandoContexto
            {
                Solicitud = solicitud,
                Comando = comando,
                Parametros = decision.ParametrosComando
            },
            cancellationToken);

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

        EntradaContextoIA entrada = await registrarContextoIAAplicacion.RegistrarEntradaAsync(
            new SolicitudRegistrarEntradaContextoIA
            {
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDRolContextoIA = RolHerramienta,
                IDTipoEntradaContextoIA = TipoEntradaResultadoComando,
                Contenido = resultadoComando.Resultado,
                FechaEntrada = DateTime.Now
            },
            cancellationToken);
        entradasContextoIA.Add(entrada);

        return ResultadoPasoContexto.Continuar();
    }

    private async Task<ResultadoPasoContexto> ProcesarHistorialAsync(
        SolicitudContextoConversacion solicitud,
        List<DatoIntermedioContexto> datosIntermedios,
        List<EntradaContextoIA> entradasContextoIA,
        CancellationToken cancellationToken)
    {
        ResultadoHistorialContexto resultadoHistorial = await proveedorHistorialContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);

        if (!resultadoHistorial.Exitoso)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError(resultadoHistorial.Error ?? "No se pudo obtener el historial."));
        }

        datosIntermedios.Add(new DatoIntermedioContexto
        {
            Tipo = "historial",
            Contenido = resultadoHistorial.Historial
        });

        EntradaContextoIA entrada = await registrarContextoIAAplicacion.RegistrarEntradaAsync(
            new SolicitudRegistrarEntradaContextoIA
            {
                IDLineaConversacion = solicitud.IDLineaConversacion,
                IDMensaje = solicitud.IDMensaje,
                IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
                IDRolContextoIA = RolHerramienta,
                IDTipoEntradaContextoIA = TipoEntradaResultadoHistorial,
                Contenido = resultadoHistorial.Historial,
                FechaEntrada = DateTime.Now
            },
            cancellationToken);
        entradasContextoIA.Add(entrada);

        return ResultadoPasoContexto.Continuar();
    }

    private async Task<ResultadoPasoContexto> ProcesarLimiteVentanaAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<EntradaContextoIA> entradasContextoIA,
        EstadoContextoConversacion? estadoContextoInicial,
        int iteracion,
        CancellationToken cancellationToken)
    {
        List<EntradaContextoIA> entradasCompactables = entradasContextoIA
            .Where(entrada => entrada.IDProcesamientoInternoMensaje != solicitud.IDProcesamientoInternoMensaje)
            .ToList();

        if (estadoContextoInicial is null && entradasCompactables.Count == 0)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError("No existe contexto anterior reducible para renovar la linea."));
        }

        ResultadoCompactacionIntencionContexto compactacion = await intencionContextoConversacionServicio.CompactarAsync(
            new SolicitudCompactacionIntencionContexto
            {
                Solicitud = solicitud,
                EstadoContextoInicial = estadoContextoInicial,
                EntradasContextoIA = entradasCompactables,
                Iteracion = iteracion
            },
            cancellationToken);

        ValidarMetadata(compactacion.Metadata, iteracion, "Compactar");
        if (!compactacion.Exitoso)
        {
            return ResultadoPasoContexto.Terminar(
                CrearError(compactacion.Error ?? "No se pudo compactar el contexto anterior."));
        }

        if (string.IsNullOrWhiteSpace(compactacion.Contenido))
        {
            return ResultadoPasoContexto.Terminar(
                CrearError("La compactacion no produjo contenido para el nuevo estado."));
        }

        return ResultadoPasoContexto.Terminar(new ResultadoContextoConversacion
        {
            TipoResultado = ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado,
            Compactacion = compactacion
        });
    }

    private static SolicitudRegistrarEntradaContextoIA CrearSolicitudEntradaDecision(
        SolicitudContextoConversacion solicitud,
        ResultadoIntencionContexto decision)
    {
        return new SolicitudRegistrarEntradaContextoIA
        {
            IDLineaConversacion = solicitud.IDLineaConversacion,
            IDMensaje = solicitud.IDMensaje,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDRolContextoIA = RolAsistente,
            IDTipoEntradaContextoIA = ObtenerTipoEntradaDecision(decision.TipoAccion),
            Contenido = decision.ContenidoDecision,
            FechaEntrada = DateTime.Now
        };
    }

    private static void ValidarContratoMetadata(ResultadoIntencionContexto decision, int iteracion)
    {
        if (decision.Metadata is null)
            throw new InvalidOperationException("La intencion de contexto debe retornar metadata IA obligatoria.");

        if (string.IsNullOrWhiteSpace(decision.ContenidoDecision))
            throw new InvalidOperationException("La intencion de contexto debe retornar contenido de decision obligatorio.");

        ValidarMetadata(decision.Metadata, iteracion, decision.TipoAccion.ToString());
    }

    private static void ValidarMetadata(
        MetadataRazonamientoIAContexto metadata,
        int iteracion,
        string accion)
    {
        if (metadata is null)
            throw new InvalidOperationException("La intencion de contexto debe retornar metadata IA obligatoria.");

        if (string.IsNullOrWhiteSpace(metadata.Proveedor))
            throw new InvalidOperationException("La metadata IA debe indicar el proveedor.");

        if (string.IsNullOrWhiteSpace(metadata.Modelo))
            throw new InvalidOperationException("La metadata IA debe indicar el modelo.");

        if (string.IsNullOrWhiteSpace(metadata.Adaptador))
            throw new InvalidOperationException("La metadata IA debe indicar el adaptador.");

        metadata.Iteracion = iteracion;
        metadata.AccionDecidida = accion;
    }

    private static string ObtenerTipoEntradaDecision(AccionContextoTipo tipoAccion)
    {
        return tipoAccion switch
        {
            AccionContextoTipo.Comando => TipoEntradaDecisionComando,
            AccionContextoTipo.Historial => TipoEntradaDecisionHistorial,
            AccionContextoTipo.Responder => TipoEntradaRespuestaFinal,
            AccionContextoTipo.NoResponder => TipoEntradaNoResponder,
            AccionContextoTipo.Error => TipoEntradaErrorIntencion,
            AccionContextoTipo.LimiteVentanaAlcanzado => TipoEntradaLimiteVentana,
            _ => TipoEntradaErrorIntencion
        };
    }

    private static List<DatoIntermedioContexto> CrearDatosIntermedios(
        IReadOnlyList<EntradaContextoIA> entradasContextoIA)
    {
        return entradasContextoIA
            .Where(entrada => entrada.IDTipoEntradaContextoIA is TipoEntradaResultadoComando or TipoEntradaResultadoHistorial)
            .Select(entrada => new DatoIntermedioContexto
            {
                Tipo = entrada.IDTipoEntradaContextoIA == TipoEntradaResultadoComando ? "comando" : "historial",
                Contenido = entrada.Contenido
            })
            .ToList();
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
}
