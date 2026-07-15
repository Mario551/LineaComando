using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class EstadoContextoConversacionAplicacion : IEstadoContextoConversacionAplicacion
{
    private readonly IUnitOfWork unitOfWork;

    public EstadoContextoConversacionAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task<EstadoContextoConversacion?> ObtenerInicialAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken)
    {
        return await (
            from linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            join estado in unitOfWork.EstadoContextoConversacionRepositorio.GetNoTracking()
                on linea.IDEstadoContextoInicial equals (long?)estado.ID
            where linea.ID == idLineaConversacion
            select new EstadoContextoConversacion
            {
                ID = estado.ID,
                IDConversacion = estado.IDConversacion,
                IDLineaConversacionOrigen = estado.IDLineaConversacionOrigen,
                IDEstadoContextoAnterior = estado.IDEstadoContextoAnterior,
                Version = estado.Version,
                Contenido = estado.Contenido,
                FechaCreacion = estado.FechaCreacion
            }).SingleOrDefaultAsync(cancellationToken);
    }
}
