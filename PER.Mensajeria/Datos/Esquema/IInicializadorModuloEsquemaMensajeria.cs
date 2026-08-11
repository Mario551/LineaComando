namespace PER.Mensajeria.Datos.Esquema;

public interface IInicializadorModuloEsquemaMensajeria
{
    Task InicializarAsync(
        ConfiguracionInicializacionEsquemaMensajeria configuracion,
        CancellationToken cancellationToken);
}
