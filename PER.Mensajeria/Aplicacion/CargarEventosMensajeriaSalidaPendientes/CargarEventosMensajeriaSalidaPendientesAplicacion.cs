namespace PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;
using PER.Mensajeria.Datos.UnitOfWork;

public class CargarEventosMensajeriaSalidaPendientesAplicacion
    : ICargarEventosMensajeriaSalidaPendientesAplicacion
{
    private const string EstadoPendiente = "pendiente";

    private readonly IUnitOfWork unitOfWork;

    public CargarEventosMensajeriaSalidaPendientesAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public Task<List<EventoMensajeriaSalida>> EjecutarAsync(
        CancellationToken cancellationToken)
    {
        return unitOfWork.EnvioMensajeRepositorio.GetNoTracking()
            .Where(envio => envio.IDEstadoEnvioMensaje == EstadoPendiente)
            .OrderBy(envio => envio.FechaCreacion)
            .ThenBy(envio => envio.ID)
            .Select(envio => new EventoMensajeriaSalida
            {
                IDEnvioMensaje = envio.ID,
                FechaCreacion = envio.FechaCreacion
            })
            .ToListAsync(cancellationToken);
    }
}
