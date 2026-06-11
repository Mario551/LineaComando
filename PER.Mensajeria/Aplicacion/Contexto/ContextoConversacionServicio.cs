namespace PER.Mensajeria.Aplicacion.Contexto;

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

    public async Task<ResultadoContextoConversacion> ResolverAsync(
        SolicitudContextoConversacion solicitud,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ComandoContexto> comandos = await proveedorCatalogoComandoContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);
        List<DatoIntermedioContexto> datosIntermedios = [];

        for (int iteracion = 1; iteracion <= configuracion.MaximoIteraciones; iteracion++)
        {
            EstadoContextoConversacion estado = new()
            {
                Solicitud = solicitud,
                Comandos = comandos,
                DatosIntermedios = datosIntermedios,
                Iteracion = iteracion
            };

            ResultadoContextoConversacion? resultadoFiltro = await EjecutarFiltrosAsync(estado, cancellationToken);
            if (resultadoFiltro is not null)
                return resultadoFiltro;

            ResultadoIntencionContexto decision = await intencionContextoConversacionServicio.DecidirAsync(
                new SolicitudIntencionContexto
                {
                    Solicitud = solicitud,
                    Comandos = comandos,
                    DatosIntermedios = datosIntermedios,
                    Iteracion = iteracion
                },
                cancellationToken);

            ResultadoContextoConversacion? resultadoFinal = await ProcesarDecisionAsync(
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

    private async Task<ResultadoContextoConversacion?> EjecutarFiltrosAsync(
        EstadoContextoConversacion estado,
        CancellationToken cancellationToken)
    {
        foreach (IFiltroContextoConversacion filtro in filtros)
        {
            ResultadoFiltroContexto resultadoFiltro = await filtro.EjecutarAsync(estado, cancellationToken);
            if (!resultadoFiltro.Continuar)
            {
                return CrearError(resultadoFiltro.Error ?? "Un filtro detuvo el contexto.");
            }
        }

        return null;
    }

    private async Task<ResultadoContextoConversacion?> ProcesarDecisionAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        List<DatoIntermedioContexto> datosIntermedios,
        ResultadoIntencionContexto decision,
        CancellationToken cancellationToken)
    {
        if (decision.TipoAccion == AccionContextoTipo.Responder)
        {
            return new ResultadoContextoConversacion
            {
                TipoResultado = ResultadoContextoConversacionTipo.ConSalidas,
                MensajesSalientes = decision.MensajesSalientes
            };
        }

        if (decision.TipoAccion == AccionContextoTipo.NoResponder)
        {
            return new ResultadoContextoConversacion
            {
                TipoResultado = ResultadoContextoConversacionTipo.SinSalidas
            };
        }

        if (decision.TipoAccion == AccionContextoTipo.Error)
        {
            return CrearError(decision.Error ?? "La IA de intencion devolvio error.");
        }

        if (decision.TipoAccion == AccionContextoTipo.Comando)
        {
            return await ProcesarComandoAsync(solicitud, comandos, datosIntermedios, decision, cancellationToken);
        }

        if (decision.TipoAccion == AccionContextoTipo.Historial)
        {
            return await ProcesarHistorialAsync(solicitud, datosIntermedios, cancellationToken);
        }

        return CrearError("Accion de contexto no soportada.");
    }

    private async Task<ResultadoContextoConversacion?> ProcesarComandoAsync(
        SolicitudContextoConversacion solicitud,
        IReadOnlyList<ComandoContexto> comandos,
        List<DatoIntermedioContexto> datosIntermedios,
        ResultadoIntencionContexto decision,
        CancellationToken cancellationToken)
    {
        ComandoContexto? comando = comandos.SingleOrDefault(
            comandoActual => comandoActual.Codigo == decision.CodigoComando && comandoActual.Autorizado);
        if (comando is null)
        {
            return CrearError($"Comando no autorizado: {decision.CodigoComando}");
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
            return CrearError(resultadoComando.Error ?? "Fallo la ejecucion del comando.");

        datosIntermedios.Add(new DatoIntermedioContexto
        {
            Tipo = "comando",
            Contenido = resultadoComando.Resultado
        });

        return null;
    }

    private async Task<ResultadoContextoConversacion?> ProcesarHistorialAsync(
        SolicitudContextoConversacion solicitud,
        List<DatoIntermedioContexto> datosIntermedios,
        CancellationToken cancellationToken)
    {
        ResultadoHistorialContexto resultadoHistorial = await proveedorHistorialContextoServicio.ObtenerAsync(
            solicitud,
            cancellationToken);

        if (!resultadoHistorial.Exitoso)
        {
            return CrearError(resultadoHistorial.Error ?? "No se pudo obtener el historial.");
        }

        datosIntermedios.Add(new DatoIntermedioContexto
        {
            Tipo = "historial",
            Contenido = resultadoHistorial.Historial
        });

        return null;
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
