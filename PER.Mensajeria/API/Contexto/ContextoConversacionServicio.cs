namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class ContextoConversacionServicio : IContextoConversacionServicio
{
    private readonly IReadOnlyList<IFiltroContextoConversacion> filtros;
    private readonly IIntencionContextoConversacionServicio intencionContextoConversacionServicio;
    private readonly IProveedorCatalogoComandoContextoServicio proveedorCatalogoComandoContextoServicio;
    private readonly IEjecutorComandoContextoServicio ejecutorComandoContextoServicio;
    private readonly IProveedorHistorialContextoServicio proveedorHistorialContextoServicio;
    private readonly ConfiguracionContextoConversacion configuracion;

    public ContextoConversacionServicio(
        IEnumerable<IFiltroContextoConversacion> filtros,
        IIntencionContextoConversacionServicio intencionContextoConversacionServicio,
        IProveedorCatalogoComandoContextoServicio proveedorCatalogoComandoContextoServicio,
        IEjecutorComandoContextoServicio ejecutorComandoContextoServicio,
        IProveedorHistorialContextoServicio proveedorHistorialContextoServicio,
        ConfiguracionContextoConversacion configuracion)
    {
        this.filtros = filtros.ToList();
        this.intencionContextoConversacionServicio = intencionContextoConversacionServicio;
        this.proveedorCatalogoComandoContextoServicio = proveedorCatalogoComandoContextoServicio;
        this.ejecutorComandoContextoServicio = ejecutorComandoContextoServicio;
        this.proveedorHistorialContextoServicio = proveedorHistorialContextoServicio;
        this.configuracion = configuracion;
    }

    public async Task<DTOResultadoContextoConversacion> ResolverAsync(
        DTOContextoConversacionSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DTOComandoContexto> comandos = await proveedorCatalogoComandoContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);
        List<DTODatoIntermedioContexto> datosIntermedios = [];

        for (int iteracion = 1; iteracion <= configuracion.MaximoIteraciones; iteracion++)
        {
            DTOContextoConversacionEstado estado = new()
            {
                Solicitud = solicitud,
                Comandos = comandos,
                DatosIntermedios = datosIntermedios,
                Iteracion = iteracion
            };

            DTOResultadoContextoConversacion? resultadoFiltro = await EjecutarFiltrosAsync(estado, cancellationToken);
            if (resultadoFiltro is not null)
                return resultadoFiltro;

            DTOIntencionContextoResultado decision = await intencionContextoConversacionServicio.DecidirAsync(
                new DTOIntencionContextoSolicitud
                {
                    Solicitud = solicitud,
                    Comandos = comandos,
                    DatosIntermedios = datosIntermedios,
                    Iteracion = iteracion
                },
                cancellationToken);

            DTOResultadoContextoConversacion? resultadoFinal = await ProcesarDecisionAsync(
                solicitud,
                comandos,
                datosIntermedios,
                decision,
                cancellationToken);

            if (resultadoFinal is not null)
            {
                return resultadoFinal;
            }
        }

        return CrearError("Se alcanzo el maximo de iteraciones del contexto.");
    }

    private async Task<DTOResultadoContextoConversacion?> EjecutarFiltrosAsync(
        DTOContextoConversacionEstado estado,
        CancellationToken cancellationToken)
    {
        foreach (IFiltroContextoConversacion filtro in filtros)
        {
            DTOResultadoFiltroContexto resultadoFiltro = await filtro.EjecutarAsync(estado, cancellationToken);
            if (!resultadoFiltro.Continuar)
            {
                return CrearError(resultadoFiltro.Error ?? "Un filtro detuvo el contexto.");
            }
        }

        return null;
    }

    private async Task<DTOResultadoContextoConversacion?> ProcesarDecisionAsync(
        DTOContextoConversacionSolicitud solicitud,
        IReadOnlyList<DTOComandoContexto> comandos,
        List<DTODatoIntermedioContexto> datosIntermedios,
        DTOIntencionContextoResultado decision,
        CancellationToken cancellationToken)
    {
        if (decision.TipoAccion == DTOAccionContextoTipo.Responder)
        {
            return new DTOResultadoContextoConversacion
            {
                TipoResultado = DTOResultadoContextoConversacionTipo.ConSalidas,
                MensajesSalientes = decision.MensajesSalientes
            };
        }

        if (decision.TipoAccion == DTOAccionContextoTipo.NoResponder)
        {
            return new DTOResultadoContextoConversacion
            {
                TipoResultado = DTOResultadoContextoConversacionTipo.SinSalidas
            };
        }

        if (decision.TipoAccion == DTOAccionContextoTipo.Error)
        {
            return CrearError(decision.Error ?? "La IA de intencion devolvio error.");
        }

        if (decision.TipoAccion == DTOAccionContextoTipo.Comando)
        {
            return await ProcesarComandoAsync(solicitud, comandos, datosIntermedios, decision, cancellationToken);
        }

        if (decision.TipoAccion == DTOAccionContextoTipo.Historial)
        {
            return await ProcesarHistorialAsync(solicitud, datosIntermedios, cancellationToken);
        }

        return CrearError("Accion de contexto no soportada.");
    }

    private async Task<DTOResultadoContextoConversacion?> ProcesarComandoAsync(
        DTOContextoConversacionSolicitud solicitud,
        IReadOnlyList<DTOComandoContexto> comandos,
        List<DTODatoIntermedioContexto> datosIntermedios,
        DTOIntencionContextoResultado decision,
        CancellationToken cancellationToken)
    {
        DTOComandoContexto? comando = comandos.SingleOrDefault(
            comandoActual => comandoActual.Codigo == decision.CodigoComando && comandoActual.Autorizado);
        if (comando is null)
        {
            return CrearError($"Comando no autorizado: {decision.CodigoComando}");
        }

        DTOResultadoComandoContexto resultadoComando = await ejecutorComandoContextoServicio.EjecutarAsync(
            new DTOEjecutarComandoContextoSolicitud
            {
                Solicitud = solicitud,
                Comando = comando,
                Parametros = decision.ParametrosComando
            },
            cancellationToken);

        if (!resultadoComando.Exitoso)
            return CrearError(resultadoComando.Error ?? "Fallo la ejecucion del comando.");

        datosIntermedios.Add(new DTODatoIntermedioContexto
        {
            Tipo = "comando",
            Contenido = resultadoComando.Resultado
        });

        return null;
    }

    private async Task<DTOResultadoContextoConversacion?> ProcesarHistorialAsync(
        DTOContextoConversacionSolicitud solicitud,
        List<DTODatoIntermedioContexto> datosIntermedios,
        CancellationToken cancellationToken)
    {
        DTOResultadoHistorialContexto resultadoHistorial = await proveedorHistorialContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);

        if (!resultadoHistorial.Exitoso)
        {
            return CrearError(resultadoHistorial.Error ?? "No se pudo obtener el historial.");
        }

        datosIntermedios.Add(new DTODatoIntermedioContexto
        {
            Tipo = "historial",
            Contenido = resultadoHistorial.Historial
        });

        return null;
    }

    private static DTOResultadoContextoConversacion CrearError(string error)
    {
        return new DTOResultadoContextoConversacion
        {
            TipoResultado = DTOResultadoContextoConversacionTipo.Error,
            Error = error
        };
    }
}
