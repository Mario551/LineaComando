using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class ConsultaMensajesLineaConversacionAnteriorAplicacion : IConsultaMensajesLineaConversacionAnteriorAplicacion
{
    private static readonly string[] EstadosTerminales = ["procesado", "error"];

    private readonly IUnitOfWorkFactory unitOfWorkFactory;
    private readonly IRegistrarContextoIAAplicacion registrarContextoIAAplicacion;

    public ConsultaMensajesLineaConversacionAnteriorAplicacion(
        IUnitOfWorkFactory unitOfWorkFactory,
        IRegistrarContextoIAAplicacion registrarContextoIAAplicacion)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
        this.registrarContextoIAAplicacion = registrarContextoIAAplicacion;
    }

    public async Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerCicloAsync(
        long idConversacion,
        long idLineaConversacionActual,
        int ciclosHaciaAtras,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ciclosHaciaAtras);

        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOLineaConversacion lineaActual = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            .SingleAsync(linea => linea.ID == idLineaConversacionActual, cancellationToken);
        ValidarConversacion(idConversacion, lineaActual);

        long? idProcesamiento = await (
            from procesamiento in unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on procesamiento.IDMensaje equals mensaje.ID
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                on mensaje.IDLineaConversacion equals linea.ID
            where linea.IDConversacion == idConversacion
                && linea.ID != lineaActual.ID
                && (linea.FechaInicio < lineaActual.FechaInicio
                    || (linea.FechaInicio == lineaActual.FechaInicio && linea.ID < lineaActual.ID))
                && EstadosTerminales.Contains(procesamiento.IDEstadoProcesamientoInternoMensaje)
            orderby linea.FechaInicio descending, linea.ID descending, procesamiento.FechaCreacion descending, procesamiento.ID descending
            select (long?)procesamiento.ID)
            .Skip(ciclosHaciaAtras - 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (!idProcesamiento.HasValue)
        {
            return [];
        }

        long idLineaOrigen = await (
            from procesamiento in unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on procesamiento.IDMensaje equals mensaje.ID
            where procesamiento.ID == idProcesamiento.Value
            select mensaje.IDLineaConversacion)
            .SingleAsync(cancellationToken);

        return await registrarContextoIAAplicacion.ObtenerMetadataEntradasProcesamientoAsync(
            idLineaOrigen,
            idProcesamiento.Value,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MetadataEntradaContextoIA>> ObtenerCicloReferenciadoAsync(
        long idConversacion,
        long idLineaConversacionActual,
        long idLineaConversacionOrigen,
        long idProcesamientoInternoMensaje,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;
        DAOLineaConversacion lineaActual = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            .SingleAsync(linea => linea.ID == idLineaConversacionActual, cancellationToken);
        ValidarConversacion(idConversacion, lineaActual);

        bool referenciaValida = await (
            from procesamiento in unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on procesamiento.IDMensaje equals mensaje.ID
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                on mensaje.IDLineaConversacion equals linea.ID
            where procesamiento.ID == idProcesamientoInternoMensaje
                && linea.ID == idLineaConversacionOrigen
                && linea.ID != idLineaConversacionActual
                && linea.IDConversacion == idConversacion
                && (linea.FechaInicio < lineaActual.FechaInicio
                    || (linea.FechaInicio == lineaActual.FechaInicio && linea.ID < lineaActual.ID))
                && EstadosTerminales.Contains(procesamiento.IDEstadoProcesamientoInternoMensaje)
            select procesamiento.ID)
            .AnyAsync(cancellationToken);

        if (!referenciaValida)
        {
            throw new InvalidOperationException(
                "La referencia de mensajes anteriores no pertenece a un ciclo terminal de la conversacion.");
        }

        return await registrarContextoIAAplicacion.ObtenerMetadataEntradasProcesamientoAsync(
            idLineaConversacionOrigen,
            idProcesamientoInternoMensaje,
            cancellationToken);
    }

    private static void ValidarConversacion(long idConversacion, DAOLineaConversacion lineaActual)
    {
        if (lineaActual.IDConversacion != idConversacion)
        {
            throw new InvalidOperationException("La linea actual no pertenece a la conversacion indicada.");
        }
    }
}
