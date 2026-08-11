using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;

namespace PER.Mensajeria.Aplicacion.Infobip.CargarPendientes;

public class CargarRecepcionesInfobipPendientesAplicacion :
    ICargarRecepcionesInfobipPendientesAplicacion
{
    private readonly IUnitOfWork unitOfWork;

    public CargarRecepcionesInfobipPendientesAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public Task<List<long>> EjecutarAsync(CancellationToken cancellationToken)
    {
        return unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio
            .GetNoTracking()
            .Where(procesamiento =>
                procesamiento.IDEstado == "pendiente"
                || procesamiento.IDEstado == "despachado")
            .OrderBy(procesamiento => procesamiento.FechaCreacion)
            .ThenBy(procesamiento => procesamiento.ID)
            .Select(procesamiento => procesamiento.IDWebhookReceiptInfobip)
            .ToListAsync(cancellationToken);
    }
}
