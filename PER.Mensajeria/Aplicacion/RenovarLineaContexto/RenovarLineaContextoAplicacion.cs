using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Aplicacion.RenovarLineaContexto;

public class RenovarLineaContextoAplicacion : IRenovarLineaContextoAplicacion
{
    private const string EstadoPendiente = "pendiente";
    private const string TipoEntradaLimiteVentana = "limite_ventana";

    private readonly IUnitOfWork unitOfWork;

    public RenovarLineaContextoAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
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

        DAOLineaConversacion? lineaOrigen = null;
        DAOMensaje? mensaje = null;
        DAOProcesamientoInternoMensaje? procesamiento = null;
        DAOMetadataRazonamientoIALineaConversacion? metadataCompactacion = null;
        DAOEstadoContextoConversacion? estadoContexto = null;
        DAOLineaConversacion? lineaNueva = null;
        List<DAOEntradaContextoIA> entradasProcesamiento = [];
        List<DAOMetadataRazonamientoIALineaConversacion> metadataProcesamiento = [];

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            lineaOrigen = await unitOfWork.LineaConversacionRepositorio.Get()
                .SingleAsync(linea => linea.ID == solicitud.IDLineaConversacionOrigen, cancellationToken);
            mensaje = await unitOfWork.MensajeRepositorio.Get()
                .SingleAsync(mensajeActual => mensajeActual.ID == solicitud.IDMensaje, cancellationToken);
            procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
                .SingleAsync(procesamientoActual => procesamientoActual.ID == solicitud.IDProcesamientoInternoMensaje, cancellationToken);

            ValidarEntidades(solicitud, lineaOrigen, mensaje, procesamiento);

            entradasProcesamiento = await unitOfWork.EntradaContextoIARepositorio.Get()
                .Where(entrada => entrada.IDProcesamientoInternoMensaje == solicitud.IDProcesamientoInternoMensaje)
                .OrderBy(entrada => entrada.Orden)
                .ThenBy(entrada => entrada.ID)
                .ToListAsync(cancellationToken);

            HashSet<long> idsMetadataMovible = entradasProcesamiento
                .Where(entrada => entrada.IDTipoEntradaContextoIA != TipoEntradaLimiteVentana)
                .Where(entrada => entrada.IDMetadataRazonamientoIA.HasValue)
                .Select(entrada => entrada.IDMetadataRazonamientoIA!.Value)
                .ToHashSet();

            metadataProcesamiento = await unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.Get()
                .Where(metadata => idsMetadataMovible.Contains(metadata.ID))
                .ToListAsync(cancellationToken);

            metadataCompactacion = CrearMetadataCompactacion(solicitud);
            await unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.AgregarAsync(
                metadataCompactacion,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            int version = await unitOfWork.EstadoContextoConversacionRepositorio.GetNoTracking()
                .Where(estado => estado.IDConversacion == solicitud.IDConversacion)
                .Select(estado => (int?)estado.Version)
                .MaxAsync(cancellationToken) ?? 0;

            estadoContexto = new DAOEstadoContextoConversacion
            {
                IDConversacion = solicitud.IDConversacion,
                IDLineaConversacionOrigen = lineaOrigen.ID,
                IDEstadoContextoAnterior = lineaOrigen.IDEstadoContextoInicial,
                IDMetadataRazonamientoIA = metadataCompactacion.ID,
                Version = version + 1,
                Contenido = solicitud.Compactacion.Contenido,
                FechaCreacion = DateTime.Now
            };
            await unitOfWork.EstadoContextoConversacionRepositorio.AgregarAsync(estadoContexto, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            lineaOrigen.Activa = false;
            lineaOrigen.FechaUltimaActividad = DateTime.Now;
            unitOfWork.LineaConversacionRepositorio.Actualizar(lineaOrigen);

            lineaNueva = new DAOLineaConversacion
            {
                IDConversacion = solicitud.IDConversacion,
                IDEstadoContextoInicial = estadoContexto.ID,
                FechaInicio = mensaje.FechaMensaje,
                FechaUltimaActividad = DateTime.Now,
                Activa = true
            };
            await unitOfWork.LineaConversacionRepositorio.AgregarAsync(lineaNueva, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            mensaje.IDLineaConversacion = lineaNueva.ID;
            unitOfWork.MensajeRepositorio.Actualizar(mensaje);

            int orden = 1;
            foreach (DAOEntradaContextoIA entrada in entradasProcesamiento)
            {
                if (entrada.IDTipoEntradaContextoIA == TipoEntradaLimiteVentana)
                {
                    continue;
                }

                entrada.IDLineaConversacion = lineaNueva.ID;
                entrada.Orden = orden;
                orden++;
                unitOfWork.EntradaContextoIARepositorio.Actualizar(entrada);
            }

            foreach (DAOMetadataRazonamientoIALineaConversacion metadata in metadataProcesamiento)
            {
                metadata.IDLineaConversacion = lineaNueva.ID;
                unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.Actualizar(metadata);
            }

            procesamiento.IDEstadoProcesamientoInternoMensaje = EstadoPendiente;
            procesamiento.Error = null;
            procesamiento.FechaProcesado = null;
            unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return CrearResultado(estadoContexto, lineaNueva, mensaje, procesamiento);
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
                lineaOrigen,
                mensaje,
                procesamiento,
                metadataCompactacion,
                estadoContexto,
                lineaNueva,
                entradasProcesamiento,
                metadataProcesamiento);
        }
    }

    private async Task<ResultadoRenovarLineaContexto?> ObtenerResultadoExistenteAsync(
        SolicitudRenovarLineaContexto solicitud,
        CancellationToken cancellationToken)
    {
        return await (
            from estado in unitOfWork.EstadoContextoConversacionRepositorio.GetNoTracking()
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                on estado.ID equals linea.IDEstadoContextoInicial
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on linea.ID equals mensaje.IDLineaConversacion
            join procesamiento in unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
                on mensaje.ID equals procesamiento.IDMensaje
            where estado.IDLineaConversacionOrigen == solicitud.IDLineaConversacionOrigen
                && mensaje.ID == solicitud.IDMensaje
                && procesamiento.ID == solicitud.IDProcesamientoInternoMensaje
            select new ResultadoRenovarLineaContexto
            {
                IDEstadoContexto = estado.ID,
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
            throw new InvalidOperationException("La compactacion debe contener el estado inicial de la nueva linea.");
        }
    }

    private static void ValidarEntidades(
        SolicitudRenovarLineaContexto solicitud,
        DAOLineaConversacion linea,
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        if (linea.IDConversacion != solicitud.IDConversacion)
        {
            throw new InvalidOperationException("La linea no pertenece a la conversacion indicada.");
        }

        if (!linea.Activa)
        {
            throw new InvalidOperationException("La linea de origen ya no esta activa.");
        }

        if (mensaje.IDLineaConversacion != linea.ID)
        {
            throw new InvalidOperationException("El mensaje no pertenece a la linea de origen.");
        }

        if (procesamiento.IDMensaje != mensaje.ID)
        {
            throw new InvalidOperationException("El procesamiento no pertenece al mensaje indicado.");
        }
    }

    private static DAOMetadataRazonamientoIALineaConversacion CrearMetadataCompactacion(
        SolicitudRenovarLineaContexto solicitud)
    {
        MetadataRazonamientoIAContexto metadata = solicitud.Compactacion.Metadata;

        return new DAOMetadataRazonamientoIALineaConversacion
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
        DAOEstadoContextoConversacion estado,
        DAOLineaConversacion linea,
        DAOMensaje mensaje,
        DAOProcesamientoInternoMensaje procesamiento)
    {
        return new ResultadoRenovarLineaContexto
        {
            IDEstadoContexto = estado.ID,
            IDLineaConversacion = linea.ID,
            IDMensaje = mensaje.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDConversacion = linea.IDConversacion
        };
    }

    private void LiberarRastreo(
        DAOLineaConversacion? lineaOrigen,
        DAOMensaje? mensaje,
        DAOProcesamientoInternoMensaje? procesamiento,
        DAOMetadataRazonamientoIALineaConversacion? metadataCompactacion,
        DAOEstadoContextoConversacion? estadoContexto,
        DAOLineaConversacion? lineaNueva,
        IReadOnlyList<DAOEntradaContextoIA> entradasProcesamiento,
        IReadOnlyList<DAOMetadataRazonamientoIALineaConversacion> metadataProcesamiento)
    {
        if (lineaOrigen is not null)
        {
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(lineaOrigen);
        }

        if (mensaje is not null)
        {
            unitOfWork.MensajeRepositorio.LiberarRastreo(mensaje);
        }

        if (procesamiento is not null)
        {
            unitOfWork.ProcesamientoInternoMensajeRepositorio.LiberarRastreo(procesamiento);
        }

        if (metadataCompactacion is not null)
        {
            unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.LiberarRastreo(metadataCompactacion);
        }

        if (estadoContexto is not null)
        {
            unitOfWork.EstadoContextoConversacionRepositorio.LiberarRastreo(estadoContexto);
        }

        if (lineaNueva is not null)
        {
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(lineaNueva);
        }

        foreach (DAOEntradaContextoIA entrada in entradasProcesamiento)
        {
            unitOfWork.EntradaContextoIARepositorio.LiberarRastreo(entrada);
        }

        foreach (DAOMetadataRazonamientoIALineaConversacion metadata in metadataProcesamiento)
        {
            unitOfWork.MetadataRazonamientoIALineaConversacionRepositorio.LiberarRastreo(metadata);
        }
    }
}
