namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

public interface IRegistrarMensajeSalidaAplicacion
{
    Task<ResultadoRegistrarMensajeSalida> EjecutarAsync(
        SolicitudRegistrarMensajeSalida solicitud,
        CancellationToken cancellationToken);
}
