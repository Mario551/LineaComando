using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Aplicacion.RenovarLineaContexto;

public class RenovarLineaContextoAplicacion : IRenovarLineaContextoAplicacion
{
    private const string EstadoPendiente = "pendiente";
    private const string TipoEntradaLimiteVentana = "limite_ventana";
    private const string TipoResultadoConsultaMensajesLineaAnterior = "resultado_consulta_mensajes_linea_anterior";

    private readonly IUnitOfWorkFactory unitOfWorkFactory;

    public RenovarLineaContextoAplicacion(IUnitOfWorkFactory unitOfWorkFactory)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<ResultadoRenovarLineaContexto> EjecutarAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken)
    {
        ValidarSolicitud(solicitud);

        ResultadoRenovarLineaContexto? resultadoExistente = await ObtenerResultadoExistenteAsync(
            solicitud,
            cancellationToken);
        if (resultadoExistente is not null)
        {
            return resultadoExistente;
        }

        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        DAOLineaConversacion? lineaOrigen = null;
        List<DAOMensaje> mensajes = [];
        List<DAOProcesamientoInternoMensaje> procesamientos = [];
        List<DAOInformacionTecnicaLlamadaIALineaConversacion> informacionesTecnicasCompactacion = [];
        DAOCompactacionContextoConversacion? compactacionContexto = null;
        DAOLineaConversacion? lineaNueva = null;
        List<DAOMetadataEntradaContextoIA> entradasProcesamiento = [];
        List<DAOInformacionTecnicaLlamadaIALineaConversacion> informacionTecnicaProcesamiento = [];
        List<DAOEjecucionComandoContexto> ejecucionesComando = [];

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            lineaOrigen = await unitOfWork.LineaConversacionRepositorio.Get()
                .SingleAsync(linea => linea.ID == solicitud.IDLineaConversacionOrigen, cancellationToken);
            IReadOnlyList<long> idsMensajes = ObtenerIDsMensajes(solicitud);
            IReadOnlyList<long> idsProcesamientos = ObtenerIDsProcesamientos(solicitud);
            mensajes = await unitOfWork.MensajeRepositorio.Get()
                .Where(mensaje => idsMensajes.Contains(mensaje.ID))
                .ToListAsync(cancellationToken);
            procesamientos = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
                .Where(procesamiento => idsProcesamientos.Contains(procesamiento.ID))
                .ToListAsync(cancellationToken);

            ValidarEntidades(solicitud, lineaOrigen, mensajes, procesamientos);

            entradasProcesamiento = await unitOfWork.MetadataEntradaContextoIARepositorio.Get()
                .Where(entrada =>
                    entrada.IDProcesamientoInternoMensaje.HasValue
                    && idsProcesamientos.Contains(entrada.IDProcesamientoInternoMensaje.Value))
                .OrderBy(entrada => entrada.Orden)
                .ThenBy(entrada => entrada.ID)
                .ToListAsync(cancellationToken);

            HashSet<long> idsInformacionTecnicaMovible = entradasProcesamiento
                .Where(entrada => entrada.IDTipoEntradaContextoIA != TipoEntradaLimiteVentana)
                .Where(entrada => entrada.IDInformacionTecnicaLlamadaIA.HasValue)
                .Select(entrada => entrada.IDInformacionTecnicaLlamadaIA!.Value)
                .ToHashSet();

            informacionTecnicaProcesamiento = await unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.Get()
                .Where(metadata => idsInformacionTecnicaMovible.Contains(metadata.ID))
                .ToListAsync(cancellationToken);

            ejecucionesComando = await unitOfWork.EjecucionComandoContextoRepositorio.Get()
                .Where(ejecucion => idsProcesamientos.Contains(ejecucion.IDProcesamientoInternoMensaje))
                .ToListAsync(cancellationToken);

            informacionesTecnicasCompactacion = solicitud.Compactacion.InformacionesTecnicasLlamadasIA
                .Select(metadata => CrearInformacionTecnicaCompactacion(solicitud, metadata))
                .ToList();
            foreach (DAOInformacionTecnicaLlamadaIALineaConversacion informacionTecnicaCompactacion in informacionesTecnicasCompactacion)
            {
                await unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.AgregarAsync(
                    informacionTecnicaCompactacion,
                    cancellationToken);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);

            int version = await unitOfWork.CompactacionContextoConversacionRepositorio.GetNoTracking()
                .Where(compactacion => compactacion.IDConversacion == solicitud.IDConversacion)
                .Select(compactacion => (int?)compactacion.Version)
                .MaxAsync(cancellationToken) ?? 0;

            compactacionContexto = new DAOCompactacionContextoConversacion
            {
                IDConversacion = solicitud.IDConversacion,
                IDLineaConversacionOrigen = lineaOrigen.ID,
                IDCompactacionContextoAnterior = lineaOrigen.IDCompactacionContextoInicial,
                IDInformacionTecnicaLlamadaIA = informacionesTecnicasCompactacion[^1].ID,
                Version = version + 1,
                Contenido = solicitud.Compactacion.Contenido,
                FechaCreacion = DateTime.Now
            };
            await unitOfWork.CompactacionContextoConversacionRepositorio.AgregarAsync(compactacionContexto, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            lineaOrigen.Activa = false;
            lineaOrigen.FechaUltimaActividad = DateTime.Now;
            unitOfWork.LineaConversacionRepositorio.Actualizar(lineaOrigen);

            lineaNueva = new DAOLineaConversacion
            {
                IDConversacion = solicitud.IDConversacion,
                IDCompactacionContextoInicial = compactacionContexto.ID,
                FechaInicio = mensajes.Min(mensaje => mensaje.FechaMensaje),
                FechaUltimaActividad = DateTime.Now,
                Activa = true
            };
            await unitOfWork.LineaConversacionRepositorio.AgregarAsync(lineaNueva, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (DAOMensaje mensaje in mensajes)
            {
                mensaje.IDLineaConversacion = lineaNueva.ID;
                unitOfWork.MensajeRepositorio.Actualizar(mensaje);
            }

            int orden = 1;
            foreach (DAOMetadataEntradaContextoIA entrada in entradasProcesamiento)
            {
                if (entrada.IDTipoEntradaContextoIA == TipoEntradaLimiteVentana)
                {
                    continue;
                }

                entrada.IDLineaConversacion = lineaNueva.ID;
                entrada.Orden = orden;
                if (entrada.IDTipoEntradaContextoIA == TipoResultadoConsultaMensajesLineaAnterior
                    && EsConsultaCargada(entrada.Contenido))
                {
                    entrada.IDCompactacionContextoIncorporada = compactacionContexto.ID;
                }
                orden++;
                unitOfWork.MetadataEntradaContextoIARepositorio.Actualizar(entrada);
            }

            foreach (DAOInformacionTecnicaLlamadaIALineaConversacion metadata in informacionTecnicaProcesamiento)
            {
                metadata.IDLineaConversacion = lineaNueva.ID;
                unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.Actualizar(metadata);
            }

            foreach (DAOEjecucionComandoContexto ejecucion in ejecucionesComando)
            {
                ejecucion.IDLineaConversacion = lineaNueva.ID;
                unitOfWork.EjecucionComandoContextoRepositorio.Actualizar(ejecucion);
            }

            foreach (DAOProcesamientoInternoMensaje procesamiento in procesamientos)
            {
                procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoPendiente;
                procesamiento.Error = null;
                procesamiento.FechaProcesado = null;
                unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            DAOMensaje mensajeCoordinador = mensajes.Single(mensaje => mensaje.ID == solicitud.IDMensaje);
            DAOProcesamientoInternoMensaje procesamientoCoordinador = procesamientos.Single(
                procesamiento => procesamiento.ID == solicitud.IDProcesamientoInternoMensaje);
            return CrearResultado(
                compactacionContexto,
                lineaNueva,
                mensajeCoordinador,
                procesamientoCoordinador);
        }
        catch
        {
            try
            {
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
        finally
        {
            LiberarRastreo(
                unitOfWork,
                lineaOrigen,
                mensajes,
                procesamientos,
                informacionesTecnicasCompactacion,
                compactacionContexto,
                lineaNueva,
                entradasProcesamiento,
                informacionTecnicaProcesamiento,
                ejecucionesComando);
        }
    }

    private async Task<ResultadoRenovarLineaContexto?> ObtenerResultadoExistenteAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        return await (
            from compactacion in unitOfWork.CompactacionContextoConversacionRepositorio.GetNoTracking()
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                on compactacion.ID equals linea.IDCompactacionContextoInicial
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on linea.ID equals mensaje.IDLineaConversacion
            join procesamiento in unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
                on mensaje.ID equals procesamiento.IDMensaje
            where compactacion.IDLineaConversacionOrigen == solicitud.IDLineaConversacionOrigen
                && mensaje.ID == solicitud.IDMensaje
                && procesamiento.ID == solicitud.IDProcesamientoInternoMensaje
            select new ResultadoRenovarLineaContexto
            {
                IDCompactacionContexto = compactacion.ID,
                IDLineaConversacion = linea.ID,
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDConversacion = linea.IDConversacion
            }).SingleOrDefaultAsync(cancellationToken);
    }

    private static void ValidarSolicitud(SolicitudRenovarLineaContexto solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Compactacion);

        if (!solicitud.Compactacion.Exitoso)
        {
            throw new InvalidOperationException("No se puede renovar una linea con una compactacion fallida.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.Compactacion.Contenido))
        {
            throw new InvalidOperationException("La compactacion debe contener el contexto inicial de la nueva linea.");
        }

        IReadOnlyList<long> idsMensajes = ObtenerIDsMensajes(solicitud);
        IReadOnlyList<long> idsProcesamientos = ObtenerIDsProcesamientos(solicitud);
        if (idsMensajes.Count != idsProcesamientos.Count)
        {
            throw new InvalidOperationException(
                "La renovacion debe recibir la misma cantidad de mensajes y procesamientos.");
        }
    }

    private static bool EsConsultaCargada(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return false;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(contenido);
            return documento.RootElement.TryGetProperty("estado", out JsonElement estado)
                && estado.ValueKind == JsonValueKind.String
                && estado.GetString() == "cargada";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidarEntidades(
        SolicitudRenovarLineaContexto solicitud,
        DAOLineaConversacion linea,
        IReadOnlyList<DAOMensaje> mensajes,
        IReadOnlyList<DAOProcesamientoInternoMensaje> procesamientos)
    {
        if (linea.IDConversacion != solicitud.IDConversacion)
        {
            throw new InvalidOperationException("La linea no pertenece a la conversacion indicada.");
        }

        if (!linea.Activa)
        {
            throw new InvalidOperationException("La linea de origen ya no esta activa.");
        }

        IReadOnlyList<long> idsMensajes = ObtenerIDsMensajes(solicitud);
        IReadOnlyList<long> idsProcesamientos = ObtenerIDsProcesamientos(solicitud);
        if (mensajes.Count != idsMensajes.Count)
        {
            throw new InvalidOperationException("No se encontraron todos los mensajes del lote a renovar.");
        }

        if (procesamientos.Count != idsProcesamientos.Count)
        {
            throw new InvalidOperationException("No se encontraron todos los procesamientos del lote a renovar.");
        }

        if (mensajes.Any(mensaje => mensaje.IDLineaConversacion != linea.ID))
        {
            throw new InvalidOperationException("Todos los mensajes deben pertenecer a la linea de origen.");
        }

        HashSet<long> idsMensajesEncontrados = mensajes.Select(mensaje => mensaje.ID).ToHashSet();
        HashSet<long> idsMensajesProcesamientos = procesamientos
            .Select(procesamiento => procesamiento.IDMensaje)
            .ToHashSet();
        if (!idsMensajesProcesamientos.SetEquals(idsMensajesEncontrados))
        {
            throw new InvalidOperationException(
                "Cada mensaje del lote debe tener exactamente un procesamiento asociado.");
        }

        if (!idsMensajesEncontrados.Contains(solicitud.IDMensaje)
            || procesamientos.All(procesamiento => procesamiento.ID != solicitud.IDProcesamientoInternoMensaje))
        {
            throw new InvalidOperationException("El mensaje y el procesamiento coordinadores no pertenecen al lote.");
        }
    }

    private static DAOInformacionTecnicaLlamadaIALineaConversacion CrearInformacionTecnicaCompactacion(
        SolicitudRenovarLineaContexto solicitud,
        InformacionTecnicaLlamadaIAContexto metadata)
    {
        return new DAOInformacionTecnicaLlamadaIALineaConversacion
        {
            IDLineaConversacion = solicitud.IDLineaConversacionOrigen,
            IDProcesamientoInternoMensaje = solicitud.IDProcesamientoInternoMensaje,
            IDMensaje = solicitud.IDMensaje,
            Proveedor = metadata.Proveedor,
            Modelo = metadata.Modelo,
            Adaptador = metadata.Adaptador,
            Iteracion = metadata.Iteracion,
            AccionDecidida = metadata.AccionDecidida,
            FinishReason = metadata.FinishReason,
            NativeFinishReason = metadata.NativeFinishReason,
            PromptTokens = metadata.PromptTokens,
            CompletionTokens = metadata.CompletionTokens,
            ReasoningTokens = metadata.ReasoningTokens,
            TotalTokens = metadata.TotalTokens,
            RequestJson = metadata.RequestJson,
            ResponseJson = metadata.ResponseJson,
            Content = metadata.Content,
            Reasoning = metadata.Reasoning,
            ReasoningDetailsJson = metadata.ReasoningDetailsJson,
            Error = metadata.Error,
            FechaCreacion = DateTime.Now
        };
    }

    private static ResultadoRenovarLineaContexto CrearResultado(
        DAOCompactacionContextoConversacion compactacion,
        DAOLineaConversacion linea,
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        return new ResultadoRenovarLineaContexto
        {
            IDCompactacionContexto = compactacion.ID,
            IDLineaConversacion = linea.ID,
            IDMensaje = mensaje.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDConversacion = linea.IDConversacion
        };
    }

    private static void LiberarRastreo(
        IUnitOfWork unitOfWork,
        DAOLineaConversacion? lineaOrigen,
        IReadOnlyList<DAOMensaje> mensajes,
        IReadOnlyList<DAOProcesamientoInternoMensaje> procesamientos,
        IReadOnlyList<DAOInformacionTecnicaLlamadaIALineaConversacion> informacionesTecnicasCompactacion,
        DAOCompactacionContextoConversacion? compactacionContexto,
        DAOLineaConversacion? lineaNueva,
        IReadOnlyList<DAOMetadataEntradaContextoIA> entradasProcesamiento,
        IReadOnlyList<DAOInformacionTecnicaLlamadaIALineaConversacion> informacionTecnicaProcesamiento,
        IReadOnlyList<DAOEjecucionComandoContexto> ejecucionesComando)
    {
        if (lineaOrigen is not null)
        {
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(lineaOrigen);
        }

        foreach (DAOMensaje mensaje in mensajes)
        {
            unitOfWork.MensajeRepositorio.LiberarRastreo(mensaje);
        }

        foreach (DAOProcesamientoInternoMensaje procesamiento in procesamientos)
        {
            unitOfWork.ProcesamientoInternoMensajeRepositorio.LiberarRastreo(procesamiento);
        }

        foreach (DAOInformacionTecnicaLlamadaIALineaConversacion informacionTecnicaCompactacion in informacionesTecnicasCompactacion)
        {
            unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.LiberarRastreo(informacionTecnicaCompactacion);
        }

        if (compactacionContexto is not null)
        {
            unitOfWork.CompactacionContextoConversacionRepositorio.LiberarRastreo(compactacionContexto);
        }

        if (lineaNueva is not null)
        {
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(lineaNueva);
        }

        foreach (DAOMetadataEntradaContextoIA entrada in entradasProcesamiento)
        {
            unitOfWork.MetadataEntradaContextoIARepositorio.LiberarRastreo(entrada);
        }

        foreach (DAOInformacionTecnicaLlamadaIALineaConversacion metadata in informacionTecnicaProcesamiento)
        {
            unitOfWork.InformacionTecnicaLlamadaIALineaConversacionRepositorio.LiberarRastreo(metadata);
        }

        foreach (DAOEjecucionComandoContexto ejecucion in ejecucionesComando)
        {
            unitOfWork.EjecucionComandoContextoRepositorio.LiberarRastreo(ejecucion);
        }
    }

    private static IReadOnlyList<long> ObtenerIDsMensajes(
        SolicitudRenovarLineaContexto solicitud)
    {
        if (solicitud.IDsMensajes.Count > 0)
        {
            return solicitud.IDsMensajes.Distinct().ToList();
        }

        return [solicitud.IDMensaje];
    }

    private static IReadOnlyList<long> ObtenerIDsProcesamientos(
        SolicitudRenovarLineaContexto solicitud)
    {
        if (solicitud.IDsProcesamientosInternosMensaje.Count > 0)
        {
            return solicitud.IDsProcesamientosInternosMensaje.Distinct().ToList();
        }

        return [solicitud.IDProcesamientoInternoMensaje];
    }
}
