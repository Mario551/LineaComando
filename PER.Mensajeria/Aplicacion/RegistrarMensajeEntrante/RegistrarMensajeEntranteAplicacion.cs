namespace PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;

public class RegistrarMensajeEntranteAplicacion : IRegistrarMensajeEntranteAplicacion
{
    public Task EjecutarAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
