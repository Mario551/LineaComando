namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

public class OrquestarMensajeEntradaAplicacion : IOrquestarMensajeEntradaAplicacion
{
    public Task EjecutarAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
