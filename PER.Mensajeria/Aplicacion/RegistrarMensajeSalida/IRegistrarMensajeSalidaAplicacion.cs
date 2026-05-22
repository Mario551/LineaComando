namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

public interface IRegistrarMensajeSalidaAplicacion
{
    Task EjecutarAsync(CancellationToken cancellationToken);
}
