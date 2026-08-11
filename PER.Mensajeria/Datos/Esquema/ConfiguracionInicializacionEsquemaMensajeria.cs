namespace PER.Mensajeria.Datos.Esquema;

public class ConfiguracionInicializacionEsquemaMensajeria
{
    public ProveedorBaseDatosMensajeria Proveedor { get; set; }
    public string CadenaConexion { get; set; } = string.Empty;
    public string? Esquema { get; set; }

    public string ObtenerEsquema()
    {
        if (!string.IsNullOrWhiteSpace(Esquema))
        {
            return Esquema;
        }

        return Proveedor switch
        {
            ProveedorBaseDatosMensajeria.PostgreSql => "public",
            ProveedorBaseDatosMensajeria.SqlServer => "dbo",
            _ => throw new InvalidOperationException(
                "No se ha configurado el proveedor de base de datos de Mensajeria.")
        };
    }

    public void Validar()
    {
        if (Proveedor == ProveedorBaseDatosMensajeria.NoConfigurado)
        {
            throw new InvalidOperationException(
                "No se ha configurado el proveedor de base de datos de Mensajeria.");
        }

        if (string.IsNullOrWhiteSpace(CadenaConexion))
        {
            throw new InvalidOperationException(
                "No se ha configurado la cadena de conexion de Mensajeria.");
        }
    }
}
