namespace PER.Mensajeria.Datos.Esquema;

public class InicializadorEsquemaMensajeriaCompuesto : IInicializadorEsquemaMensajeria
{
    private readonly ConfiguracionInicializacionEsquemaMensajeria configuracion;
    private readonly IEnumerable<IInicializadorModuloEsquemaMensajeria> inicializadoresModulos;

    public InicializadorEsquemaMensajeriaCompuesto(
        ConfiguracionInicializacionEsquemaMensajeria configuracion,
        IEnumerable<IInicializadorModuloEsquemaMensajeria> inicializadoresModulos)
    {
        this.configuracion = configuracion;
        this.inicializadoresModulos = inicializadoresModulos;
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        configuracion.Validar();
        string esquema = configuracion.ObtenerEsquema();

        if (configuracion.Proveedor == ProveedorBaseDatosMensajeria.PostgreSql)
        {
            await new InicializadorEsquemaMensajeriaPostgres(
                configuracion.CadenaConexion,
                esquema)
                .InicializarAsync(cancellationToken);
        }
        else if (configuracion.Proveedor == ProveedorBaseDatosMensajeria.SqlServer)
        {
            await new InicializadorEsquemaMensajeriaSqlServer(
                configuracion.CadenaConexion,
                esquema)
                .InicializarAsync(cancellationToken);
        }
        else
        {
            throw new NotSupportedException(
                $"El proveedor '{configuracion.Proveedor}' no esta soportado.");
        }

        foreach (IInicializadorModuloEsquemaMensajeria inicializador in inicializadoresModulos)
        {
            await inicializador.InicializarAsync(configuracion, cancellationToken);
        }
    }
}
