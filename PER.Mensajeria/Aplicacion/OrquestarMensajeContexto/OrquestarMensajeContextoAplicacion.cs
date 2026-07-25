namespace PER.Mensajeria.Aplicacion.OrquestarMensajeContexto;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

public class OrquestarMensajeContextoAplicacion : IOrquestarMensajeContextoAplicacion
{
    private const string EstadoPendiente = "pendiente";
    private const string EstadoEnProceso = "en_proceso";
    private const string EstadoProcesado = "procesado";
    private const string EstadoError = "error";

    private readonly IUnitOfWorkFactory unitOfWorkFactory;
    private readonly IContextoConversacionServicio contextoConversacionServicio;
    private readonly IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion;
    private readonly IColaEventosMensajeriaSalidaServicio colaEventosMensajeriaSalidaServicio;
    private readonly ILogger<OrquestarMensajeContextoAplicacion> logger;

    public OrquestarMensajeContextoAplicacion(
        IUnitOfWorkFactory unitOfWorkFactory,
        IContextoConversacionServicio contextoConversacionServicio,
        IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion,
        IColaEventosMensajeriaSalidaServicio colaEventosMensajeriaSalidaServicio,
        ILogger<OrquestarMensajeContextoAplicacion> logger)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
        this.contextoConversacionServicio = contextoConversacionServicio;
        this.registrarMensajeSalidaAplicacion = registrarMensajeSalidaAplicacion;
        this.colaEventosMensajeriaSalidaServicio = colaEventosMensajeriaSalidaServicio;
        this.logger = logger;
    }

    public async Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        return await EjecutarAsync(
            new[] { idProcesamientoInternoMensaje },
            cancellationToken);
    }

    public async Task<ResultadoOrquestarMensajeContexto> EjecutarAsync(
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<long> idsProcesamientos = ValidarIDsProcesamientos(
            idsProcesamientosInternosMensaje);
        DatosProcesamientoMensaje? datosProcesamiento = await PrepararProcesamientosAsync(
            idsProcesamientos,
            cancellationToken);

        if (datosProcesamiento is null)
        {
            logger.LogDebug(
                "Lote ignorado porque todos sus procesamientos ya estan en estado terminal. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}",
                string.Join(",", idsProcesamientos));
            return ResultadoOrquestarMensajeContexto.Procesado();
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

                return ResultadoOrquestarMensajeContexto.RenovarLinea(
                    compactacion,
                    datosProcesamiento.IDMensaje,
                    datosProcesamiento.IDsMensajes,
                    datosProcesamiento.IDsProcesamientosInternosMensaje,
                    datosProcesamiento.IDConversacion,
                    datosProcesamiento.IDLineaConversacion);
            }

            foreach (MensajeSalienteContexto mensajeSaliente in resultadoContexto.MensajesSalientes)
            {
                SolicitudRegistrarMensajeSalida solicitudRegistrarSalida = CrearSolicitudRegistrarSalida(
                    mensajeSaliente,
                    datosProcesamiento.IDConversacion,
                    datosProcesamiento.IDLineaConversacion);

                ResultadoRegistrarMensajeSalida resultadoRegistro = await registrarMensajeSalidaAplicacion.EjecutarAsync(
                    solicitudRegistrarSalida,
                    cancellationToken);

                colaEventosMensajeriaSalidaServicio.Publicar(new EventoMensajeriaSalida
                {
                    IDEnvioMensaje = resultadoRegistro.IDEnvioMensaje,
                    FechaCreacion = DateTime.Now
                });
            }

            await MarcarProcesadosAsync(
                datosProcesamiento.IDsProcesamientosInternosMensaje,
                cancellationToken);

            return resultadoContexto.TipoResultado == ResultadoContextoConversacionTipo.SinSalidas
                ? ResultadoOrquestarMensajeContexto.SinSalidas()
                : ResultadoOrquestarMensajeContexto.Procesado();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Lote cancelado por apagado. Sus procesamientos se conservan en proceso para rehidratacion. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}",
                string.Join(",", datosProcesamiento.IDsProcesamientosInternosMensaje));
            throw;
        }
        catch (Exception excepcion)
        {
            logger.LogError(
                excepcion,
                "Error orquestando lote de mensajes de entrada. IDsProcesamientosInternosMensaje={IDsProcesamientosInternosMensaje}, IDsMensajes={IDsMensajes}, IDConversacion={IDConversacion}, IDLineaConversacion={IDLineaConversacion}",
                string.Join(",", datosProcesamiento.IDsProcesamientosInternosMensaje),
                string.Join(",", datosProcesamiento.IDsMensajes),
                datosProcesamiento.IDConversacion,
                datosProcesamiento.IDLineaConversacion);

            await MarcarErroresAsync(
                datosProcesamiento.IDsProcesamientosInternosMensaje,
                excepcion.Message,
                cancellationToken);
            return ResultadoOrquestarMensajeContexto.ConError(excepcion.Message);
        }
    }

    private static SolicitudRegistrarMensajeSalida CrearSolicitudRegistrarSalida(
        MensajeSalienteContexto mensajeSaliente,
        long idConversacion,
        long idLineaConversacion)
    {
        return new SolicitudRegistrarMensajeSalida
        {
            IDConversacion = idConversacion,
            IDLineaConversacion = idLineaConversacion,
            TipoMensaje = mensajeSaliente.TipoMensaje,
            TelefonoOrigen = mensajeSaliente.TelefonoOrigen,
            TelefonoDestino = mensajeSaliente.TelefonoDestino,
            Contenido = mensajeSaliente.Contenido,
            FechaMensaje = mensajeSaliente.FechaMensaje,
            Archivos = mensajeSaliente.Archivos
                .Select(archivo => new ArchivoRegistrarMensajeSalida
                {
                    NombreArchivo = archivo.NombreArchivo,
                    TipoContenido = archivo.TipoContenido,
                    TamanoBytes = archivo.TamanoBytes,
                    UbicacionArchivo = archivo.UbicacionArchivo,
                    ProveedorAlmacenamiento = archivo.ProveedorAlmacenamiento,
                    IdentificadorExternoArchivo = archivo.IdentificadorExternoArchivo
                })
                .ToList()
        };
    }

    private async Task<DatosProcesamientoMensaje?> PrepararProcesamientosAsync(
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        List<DAOProcesamientoInternoMensaje> procesamientos = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .Where(procesamiento => idsProcesamientosInternosMensaje.Contains(procesamiento.ID))
            .ToListAsync(cancellationToken);
        if (procesamientos.Count != idsProcesamientosInternosMensaje.Count)
        {
            HashSet<long> idsEncontrados = procesamientos
                .Select(procesamiento => procesamiento.ID)
                .ToHashSet();
            string idsFaltantes = string.Join(
                ",",
                idsProcesamientosInternosMensaje.Where(id => !idsEncontrados.Contains(id)));
            throw new InvalidOperationException(
                $"No se encontraron los procesamientos internos del lote: {idsFaltantes}.");
        }

        List<DAOProcesamientoInternoMensaje> procesamientosActivos = procesamientos
            .Where(procesamiento =>
                procesamiento.IDEstadoProcesamientoInternoMensaje is not EstadoProcesado and not EstadoError)
            .ToList();
        if (procesamientosActivos.Count == 0)
        {
            return null;
        }

        DAOProcesamientoInternoMensaje? procesamientoEstadoInvalido = procesamientosActivos
            .FirstOrDefault(procesamiento =>
                procesamiento.IDEstadoProcesamientoInternoMensaje is not EstadoPendiente and not EstadoEnProceso);
        if (procesamientoEstadoInvalido is not null)
        {
            throw new InvalidOperationException(
                $"El procesamiento {procesamientoEstadoInvalido.ID} tiene el estado no soportado '{procesamientoEstadoInvalido.IDEstadoProcesamientoInternoMensaje}'.");
        }

        List<long> idsMensajes = procesamientosActivos
            .Select(procesamiento => procesamiento.IDMensaje)
            .Distinct()
            .ToList();
        List<DAOMensaje> mensajesEntrada = await unitOfWork.MensajeRepositorio.Get()
            .Where(mensaje => idsMensajes.Contains(mensaje.ID))
            .ToListAsync(cancellationToken);
        if (mensajesEntrada.Count != idsMensajes.Count)
        {
            throw new InvalidOperationException(
                "No se encontraron todos los mensajes asociados a los procesamientos del lote.");
        }

        List<long> idsLineas = mensajesEntrada
            .Select(mensaje => mensaje.IDLineaConversacion)
            .Distinct()
            .ToList();
        List<DAOLineaConversacion> lineasReferenciadas = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            .Where(linea => idsLineas.Contains(linea.ID))
            .ToListAsync(cancellationToken);
        if (lineasReferenciadas.Count != idsLineas.Count)
        {
            throw new InvalidOperationException(
                "No se encontraron todas las lineas asociadas a los mensajes del lote.");
        }

        List<long> idsConversaciones = lineasReferenciadas
            .Select(linea => linea.IDConversacion)
            .Distinct()
            .ToList();
        if (idsConversaciones.Count != 1)
        {
            throw new InvalidOperationException(
                "Todos los mensajes de un lote deben pertenecer a la misma conversacion.");
        }

        long idConversacion = idsConversaciones[0];
        DAOLineaConversacion linea;
        if (lineasReferenciadas.Count == 1 && lineasReferenciadas[0].Activa)
        {
            linea = lineasReferenciadas[0];
        }
        else
        {
            linea = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                .Where(lineaActual => lineaActual.IDConversacion == idConversacion && lineaActual.Activa)
                .OrderByDescending(lineaActual => lineaActual.FechaInicio)
                .ThenByDescending(lineaActual => lineaActual.ID)
                .FirstAsync(cancellationToken);

            foreach (DAOMensaje mensajeEntrada in mensajesEntrada)
            {
                mensajeEntrada.IDLineaConversacion = linea.ID;
            }
        }

        DAOConversacion conversacion = await unitOfWork.ConversacionRepositorio.GetNoTracking()
            .SingleAsync(conversacionActual => conversacionActual.ID == linea.IDConversacion, cancellationToken);

        List<ArchivoMensajeLote> archivos = await unitOfWork.ArchivoMensajeRepositorio.GetNoTracking()
            .Where(archivo => idsMensajes.Contains(archivo.IDMensaje))
            .Select(archivo => new ArchivoMensajeLote
            {
                IDMensaje = archivo.IDMensaje,
                Archivo = new ArchivoMensajeContexto
                {
                    NombreArchivo = archivo.NombreArchivo,
                    TipoContenido = archivo.IDTipoContenidoArchivo,
                    TamanoBytes = archivo.TamanoBytes,
                    UbicacionArchivo = archivo.UbicacionArchivo,
                    ProveedorAlmacenamiento = archivo.ProveedorAlmacenamiento,
                    IdentificadorExternoArchivo = archivo.IdentificadorExternoArchivo
                }
            })
            .ToListAsync(cancellationToken);

        foreach (DAOProcesamientoInternoMensaje procesamiento in procesamientosActivos)
        {
            procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoEnProceso;
            procesamiento.Error = null;
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        Dictionary<long, DAOProcesamientoInternoMensaje> procesamientosPorMensaje = procesamientosActivos
            .ToDictionary(procesamiento => procesamiento.IDMensaje);
        List<MensajeEntranteContexto> mensajesContexto = mensajesEntrada
            .OrderBy(mensaje => mensaje.FechaMensaje)
            .ThenBy(mensaje => mensaje.ID)
            .Select(mensaje => new MensajeEntranteContexto
            {
                IDProcesamientoInternoMensaje = procesamientosPorMensaje[mensaje.ID].ID,
                IDMensaje = mensaje.ID,
                TipoMensaje = mensaje.IDTipoMensaje,
                TelefonoOrigen = mensaje.TelefonoOrigen,
                TelefonoDestino = mensaje.TelefonoDestino,
                Contenido = mensaje.Contenido,
                IdentificadorExternoMensaje = mensaje.IdentificadorExternoMensaje,
                FechaMensaje = mensaje.FechaMensaje,
                Archivos = archivos
                    .Where(archivo => archivo.IDMensaje == mensaje.ID)
                    .Select(archivo => archivo.Archivo)
                    .ToList()
            })
            .ToList();
        DAOProcesamientoInternoMensaje procesamientoCoordinador = procesamientosActivos
            .OrderBy(procesamiento => procesamiento.FechaCreacion)
            .ThenBy(procesamiento => procesamiento.ID)
            .First();
        MensajeEntranteContexto mensajeCoordinador = mensajesContexto
            .Single(mensaje => mensaje.IDMensaje == procesamientoCoordinador.IDMensaje);

        return new DatosProcesamientoMensaje
        {
            IDMensaje = mensajeCoordinador.IDMensaje,
            IDsMensajes = mensajesContexto.Select(mensaje => mensaje.IDMensaje).ToList(),
            IDsProcesamientosInternosMensaje = procesamientosActivos
                .OrderBy(procesamiento => procesamiento.FechaCreacion)
                .ThenBy(procesamiento => procesamiento.ID)
                .Select(procesamiento => procesamiento.ID)
                .ToList(),
            IDConversacion = conversacion.ID,
            IDLineaConversacion = linea.ID,
            SolicitudContexto = new SolicitudContextoConversacion
            {
                IDProcesamientoInternoMensaje = procesamientoCoordinador.ID,
                IDsProcesamientosInternosMensaje = procesamientosActivos
                    .OrderBy(procesamiento => procesamiento.FechaCreacion)
                    .ThenBy(procesamiento => procesamiento.ID)
                    .Select(procesamiento => procesamiento.ID)
                    .ToList(),
                IDMensaje = mensajeCoordinador.IDMensaje,
                IDConversacion = conversacion.ID,
                IDLineaConversacion = linea.ID,
                IDCuentaCanal = conversacion.IDCuentaCanal,
                TipoMensaje = mensajeCoordinador.TipoMensaje,
                TelefonoOrigen = mensajeCoordinador.TelefonoOrigen,
                TelefonoDestino = mensajeCoordinador.TelefonoDestino,
                Contenido = mensajeCoordinador.Contenido,
                IdentificadorExternoMensaje = mensajeCoordinador.IdentificadorExternoMensaje,
                FechaMensaje = mensajeCoordinador.FechaMensaje,
                Archivos = mensajeCoordinador.Archivos,
                MensajesEntrantes = mensajesContexto
            }
        };
    }

    private async Task MarcarProcesadosAsync(
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        List<DAOProcesamientoInternoMensaje> procesamientos = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .Where(procesamiento => idsProcesamientosInternosMensaje.Contains(procesamiento.ID))
            .ToListAsync(cancellationToken);

        DateTime fechaProcesado = DateTime.Now;
        foreach (DAOProcesamientoInternoMensaje procesamiento in procesamientos)
        {
            procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoProcesado;
            procesamiento.FechaProcesado = fechaProcesado;
            procesamiento.Error = null;
            unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task MarcarErroresAsync(
        IReadOnlyList<long> idsProcesamientosInternosMensaje,
        string error,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        List<DAOProcesamientoInternoMensaje> procesamientos = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .Where(procesamiento => idsProcesamientosInternosMensaje.Contains(procesamiento.ID))
            .ToListAsync(cancellationToken);

        foreach (DAOProcesamientoInternoMensaje procesamiento in procesamientos)
        {
            procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoError;
            procesamiento.Intentos++;
            procesamiento.Error = error;
            unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<long> ValidarIDsProcesamientos(
        IReadOnlyList<long> idsProcesamientosInternosMensaje)
    {
        ArgumentNullException.ThrowIfNull(idsProcesamientosInternosMensaje);

        List<long> ids = idsProcesamientosInternosMensaje
            .Distinct()
            .ToList();
        if (ids.Count == 0 || ids.Any(id => id <= 0))
        {
            throw new ArgumentException(
                "El lote debe contener identificadores de procesamiento validos.",
                nameof(idsProcesamientosInternosMensaje));
        }

        return ids;
    }

    private class DatosProcesamientoMensaje
    {
        public long IDMensaje { get; init; }
        public required IReadOnlyList<long> IDsMensajes { get; init; }
        public required IReadOnlyList<long> IDsProcesamientosInternosMensaje { get; init; }
        public long IDConversacion { get; init; }
        public long IDLineaConversacion { get; init; }
        public required SolicitudContextoConversacion SolicitudContexto { get; init; }
    }

    private sealed class ArchivoMensajeLote
    {
        public long IDMensaje { get; init; }
        public required ArchivoMensajeContexto Archivo { get; init; }
    }
}
