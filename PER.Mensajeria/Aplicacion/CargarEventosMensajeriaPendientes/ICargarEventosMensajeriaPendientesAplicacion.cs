
namespace PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;

public interface ICargarEventosMensajeriaPendientesAplicacion
{
    Task<List<EventoMensajeriaPendiente>> EjecutarAsync(CancellationToken cancellationToken);
}
