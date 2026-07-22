using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;

namespace PER.Mensajeria.Aplicacion.Contexto;

public class CompactacionContextoConversacionAplicacion : ICompactacionContextoConversacionAplicacion
{
    private readonly IUnitOfWorkFactory unitOfWorkFactory;

    public CompactacionContextoConversacionAplicacion(IUnitOfWorkFactory unitOfWorkFactory)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<CompactacionContextoConversacion?> ObtenerInicialAsync(
        long idLineaConversacion,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

        return await (
            from linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            join compactacion in unitOfWork.CompactacionContextoConversacionRepositorio.GetNoTracking()
                on linea.IDCompactacionContextoInicial equals (long?)compactacion.ID
            where linea.ID == idLineaConversacion
            select new CompactacionContextoConversacion
            {
                ID = compactacion.ID,
                IDConversacion = compactacion.IDConversacion,
                IDLineaConversacionOrigen = compactacion.IDLineaConversacionOrigen,
                IDCompactacionContextoAnterior = compactacion.IDCompactacionContextoAnterior,
                Version = compactacion.Version,
                Contenido = compactacion.Contenido,
                FechaCreacion = compactacion.FechaCreacion
            }).SingleOrDefaultAsync(cancellationToken);
    }
}
