namespace PER.Mensajeria.Datos.Esquema;

public interface IInicializadorEsquemaMensajeria
{
    Task InicializarAsync(CancellationToken cancellationToken = default);
}
