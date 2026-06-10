using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;

public class CargarEventosMensajeriaPendientesAplicacion : ICargarEventosMensajeriaPendientesAplicacion
{
    private readonly IUnitOfWork unitOfWork;

    public CargarEventosMensajeriaPendientesAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public Task<List<DTOEventoMensajeria>> EjecutarAsync(CancellationToken cancellationToken)
    {
        IQueryable<DTOEventoMensajeria> consulta =
            from procesamiento in unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on procesamiento.IDMensaje equals mensaje.ID
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                on mensaje.IDLineaConversacion equals linea.ID
            where procesamiento.IDTipoProcesamientoInternoMensaje == "orquestar_entrada"
                && (procesamiento.IDEstadoProcesamientoInternoMensaje == "pendiente"
                    || procesamiento.IDEstadoProcesamientoInternoMensaje == "en_proceso")
            orderby procesamiento.FechaCreacion, procesamiento.ID
            select new DTOEventoMensajeria
            {
                IDMensaje = mensaje.ID,
                IDProcesamientoInternoMensaje = procesamiento.ID,
                IDConversacion = linea.IDConversacion,
                IDLineaConversacion = linea.ID,
                FechaCreacion = procesamiento.FechaCreacion
            };

        return consulta.ToListAsync(cancellationToken);
    }
}
