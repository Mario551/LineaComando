using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;

public interface ICargarEventosMensajeriaPendientesAplicacion
{
    Task<List<DTOEventoMensajeria>> EjecutarAsync(CancellationToken cancellationToken);
}
