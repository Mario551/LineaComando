namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

public class RegistrarMensajeSalidaAplicacion : IRegistrarMensajeSalidaAplicacion
{
    public Task EjecutarAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
