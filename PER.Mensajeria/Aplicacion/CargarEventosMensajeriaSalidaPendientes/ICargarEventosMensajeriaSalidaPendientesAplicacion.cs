namespace PER.Mensajeria.Aplicacion.CargarEventosMensajeriaSalidaPendientes;

using PER.Mensajeria.Aplicacion.ColaMensajeria.Salida;

public interface ICargarEventosMensajeriaSalidaPendientesAplicacion
{
    Task<List<EventoMensajeriaSalida>> EjecutarAsync(
        CancellationToken cancellationToken);
}
