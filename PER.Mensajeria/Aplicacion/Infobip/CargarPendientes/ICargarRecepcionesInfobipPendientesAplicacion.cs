namespace PER.Mensajeria.Aplicacion.Infobip.CargarPendientes;

public interface ICargarRecepcionesInfobipPendientesAplicacion
{
    Task<List<long>> EjecutarAsync(CancellationToken cancellationToken);
}
