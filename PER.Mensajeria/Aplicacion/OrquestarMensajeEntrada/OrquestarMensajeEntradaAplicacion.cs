namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class OrquestarMensajeEntradaAplicacion : IOrquestarMensajeEntradaAplicacion
{
    private const string EstadoPendiente = "pendiente";
    private const string EstadoEnProceso = "en_proceso";
    private const string EstadoProcesado = "procesado";
    private const string EstadoError = "error";

    private readonly IUnitOfWorkFactory unitOfWorkFactory;
    private readonly IContextoConversacionServicio contextoConversacionServicio;
    private readonly IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion;
    private readonly ILogger<OrquestarMensajeEntradaAplicacion> logger;

    public OrquestarMensajeEntradaAplicacion(
        IUnitOfWorkFactory unitOfWorkFactory,
        IContextoConversacionServicio contextoConversacionServicio,
        IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion,
        ILogger<OrquestarMensajeEntradaAplicacion> logger)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
        this.contextoConversacionServicio = contextoConversacionServicio;
        this.registrarMensajeSalidaAplicacion = registrarMensajeSalidaAplicacion;
        this.logger = logger;
    }

    public async Task<ResultadoOrquestarMensajeEntrada> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        DatosProcesamientoMensaje? datosProcesamiento = await PrepararProcesamientoAsync(
            idProcesamientoInternoMensaje,
            cancellationToken);

        if (datosProcesamiento is null)
        {
            logger.LogDebug(
                "Evento ignorado porque el procesamiento ya esta en estado terminal. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}",
                idProcesamientoInternoMensaje);
            return ResultadoOrquestarMensajeEntrada.Procesado();
        }

        try
        {
            ResultadoContextoConversacion resultadoContexto = await contextoConversacionServicio.ResolverAsync(
                datosProcesamiento.SolicitudContexto,
                cancellationToken);

            if (resultadoContexto.TipoResultado == ResultadoContextoConversacionTipo.Error)
            {
                throw new InvalidOperationException(resultadoContexto.Error ?? "El contexto devolvio error.");
            }

            if (resultadoContexto.TipoResultado == ResultadoContextoConversacionTipo.LimiteVentanaAlcanzado)
            {
                ResultadoCompactacionIntencionContexto compactacion = resultadoContexto.Compactacion
                    ?? throw new InvalidOperationException("El limite de ventana debe incluir una compactacion valida.");

                return ResultadoOrquestarMensajeEntrada.RenovarLinea(
                    compactacion,
                    datosProcesamiento.IDMensaje,
                    datosProcesamiento.IDConversacion,
                    datosProcesamiento.IDLineaConversacion);
            }

            foreach (DTOMensajeSaliente mensajeSaliente in resultadoContexto.MensajesSalientes)
            {
                ForzarRelacionSalida(
                    mensajeSaliente,
                    datosProcesamiento.IDConversacion,
                    datosProcesamiento.IDLineaConversacion);

                await registrarMensajeSalidaAplicacion.EjecutarAsync(new DTORegistrarMensajeSalidaSolicitud
                {
                    Mensaje = mensajeSaliente
                }, cancellationToken);
            }

            await MarcarProcesadoAsync(idProcesamientoInternoMensaje, cancellationToken);

            return resultadoContexto.TipoResultado == ResultadoContextoConversacionTipo.SinSalidas
                ? ResultadoOrquestarMensajeEntrada.SinSalidas()
                : ResultadoOrquestarMensajeEntrada.Procesado();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Procesamiento cancelado por apagado. Se conserva en proceso para rehidratacion. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}",
                idProcesamientoInternoMensaje);
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Error orquestando mensaje de entrada. IDProcesamientoInternoMensaje={IDProcesamientoInternoMensaje}, IDMensaje={IDMensaje}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                idProcesamientoInternoMensaje,
                datosProcesamiento.IDMensaje,
                datosProcesamiento.IDConversacion,
                datosProcesamiento.IDLineaConversacion);

            await MarcarErrorAsync(idProcesamientoInternoMensaje, excepcion.Message, cancellationToken);
            return ResultadoOrquestarMensajeEntrada.ConError(excepcion.Message);
        }
    }

    private static void ForzarRelacionSalida(
        DTOMensajeSaliente mensajeSaliente,
        long idConversacion,
        long idLineaConversacion)
    {
        mensajeSaliente.IDConversacion = idConversacion;
        mensajeSaliente.IDLineaConversacion = idLineaConversacion;
    }

    private async Task<DatosProcesamientoMensaje?> PrepararProcesamientoAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        DAOProcesamientoInternoMensaje procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .SingleAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje, cancellationToken);

        if (procesamiento.IDEstadoProcesamientoInternoMensaje is EstadoProcesado or EstadoError)
        {
            return null;
        }

        if (procesamiento.IDEstadoProcesamientoInternoMensaje is not EstadoPendiente and not EstadoEnProceso)
        {
            throw new InvalidOperationException(
                $"El procesamiento {idProcesamientoInternoMensaje} tiene el estado no soportado '{procesamiento.IDEstadoProcesamientoInternoMensaje}'.");
        }

        DAOMensaje mensajeEntrada = await unitOfWork.MensajeRepositorio.Get()
            .SingleAsync(mensajeActual => mensajeActual.ID == procesamiento.IDMensaje, cancellationToken);
        DAOLineaConversacion linea = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            .SingleAsync(lineaActual => lineaActual.ID == mensajeEntrada.IDLineaConversacion, cancellationToken);

        if (!linea.Activa)
        {
            linea = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                .Where(lineaActual => lineaActual.IDConversacion == linea.IDConversacion && lineaActual.Activa)
                .OrderByDescending(lineaActual => lineaActual.FechaInicio)
                .ThenByDescending(lineaActual => lineaActual.ID)
                .FirstAsync(cancellationToken);
            mensajeEntrada.IDLineaConversacion = linea.ID;
        }

        DAOConversacion conversacion = await unitOfWork.ConversacionRepositorio.GetNoTracking()
            .SingleAsync(conversacionActual => conversacionActual.ID == linea.IDConversacion, cancellationToken);

        List<DTOArchivoMensaje> archivos = await unitOfWork.ArchivoMensajeRepositorio.GetNoTracking()
            .Where(archivoActual => archivoActual.IDMensaje == mensajeEntrada.ID)
            .Select(archivoActual => new DTOArchivoMensaje
            {
                NombreArchivo = archivoActual.NombreArchivo,
                TipoContenido = archivoActual.IDTipoContenidoArchivo,
                TamanoBytes = archivoActual.TamanoBytes,
                UbicacionArchivo = archivoActual.UbicacionArchivo,
                ProveedorAlmacenamiento = archivoActual.ProveedorAlmacenamiento,
                IdentificadorExternoArchivo = archivoActual.IdentificadorExternoArchivo
            })
            .ToListAsync(cancellationToken);

        procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoEnProceso;
        procesamiento.Error = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DatosProcesamientoMensaje
        {
            IDMensaje = mensajeEntrada.ID,
            IDConversacion = conversacion.ID,
            IDLineaConversacion = linea.ID,
            SolicitudContexto = new SolicitudContextoConversacion
            {
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDMensaje = mensajeEntrada.ID,
                IDConversacion = conversacion.ID,
                IDLineaConversacion = linea.ID,
                IDCuentaCanal = conversacion.IDCuentaCanal,
                TipoMensaje = mensajeEntrada.IDTipoMensaje,
                TelefonoOrigen = mensajeEntrada.TelefonoOrigen,
                TelefonoDestino = mensajeEntrada.TelefonoDestino,
                Contenido = mensajeEntrada.Contenido,
                IdentificadorExternoMensaje = mensajeEntrada.IdentificadorExternoMensaje,
                FechaMensaje = mensajeEntrada.FechaMensaje,
                Archivos = archivos
            }
        };
    }

    private async Task MarcarProcesadoAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOProcesamientoInternoMensaje procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .SingleAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje, cancellationToken);

        procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoProcesado;
        procesamiento.FechaProcesado = DateTime.Now;
        procesamiento.Error = null;
        unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task MarcarErrorAsync(
        long idProcesamientoInternoMensaje,
        string error,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOProcesamientoInternoMensaje procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .SingleAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje, cancellationToken);

        procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoError;
        procesamiento.Intentos++;
        procesamiento.Error = error;
        unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private class DatosProcesamientoMensaje
    {
        public long IDMensaje { get; init; }
        public long IDConversacion { get; init; }
        public long IDLineaConversacion { get; init; }
        public required SolicitudContextoConversacion SolicitudContexto { get; init; }
    }
}
