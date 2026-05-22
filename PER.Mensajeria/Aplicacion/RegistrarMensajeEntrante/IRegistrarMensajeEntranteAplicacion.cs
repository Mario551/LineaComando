namespace PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;

public interface IRegistrarMensajeEntranteAplicacion
{
    Task EjecutarAsync(CancellationToken cancellationToken);
}
