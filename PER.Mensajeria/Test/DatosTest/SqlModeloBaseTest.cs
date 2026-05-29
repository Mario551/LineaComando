namespace DatosTest;

public class SqlModeloBaseTest
{
    [Fact]
    public void TablasSql_DebeCrearSoloTablasPrefijadasConPer()
    {
        string sql = LeerTablasSql();
        string[] lineasCreateTable = sql
            .Split(Environment.NewLine)
            .Where(linea => linea.TrimStart().StartsWith("CREATE TABLE ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(lineasCreateTable);
        Assert.All(lineasCreateTable, linea => Assert.StartsWith("CREATE TABLE per_", linea.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TablasSql_DebeInsertarCatalogosBaseDelFlujo()
    {
        string sql = LeerTablasSql();

        Assert.Contains("INSERT INTO per_direcciones_mensaje", sql);
        Assert.Contains("('entrada', 'Entrada')", sql);
        Assert.Contains("('salida', 'Salida')", sql);
        Assert.Contains("INSERT INTO per_tipos_procesamiento_interno_mensaje", sql);
        Assert.Contains("('orquestar_entrada', 'Orquestar mensaje de entrada')", sql);
        Assert.Contains("('pendiente', 'Pendiente')", sql);
        Assert.Contains("('procesado', 'Procesado')", sql);
        Assert.Contains("('error', 'Error')", sql);
        Assert.Contains("('enviado', 'Enviado')", sql);
        Assert.Contains("('fallido', 'Fallido')", sql);
    }

    [Fact]
    public void TablasSql_DebeTenerRelacionesEIndicesOperativos()
    {
        string sql = LeerTablasSql();

        Assert.Contains("REFERENCES per_lineas_conversacion(id)", sql);
        Assert.Contains("REFERENCES per_mensajes(id)", sql);
        Assert.Contains("REFERENCES per_estados_procesamiento_interno_mensaje(id)", sql);
        Assert.Contains("REFERENCES per_estados_envio_mensaje(id)", sql);
        Assert.Contains("CREATE UNIQUE INDEX ux_mensajes_idempotencia", sql);
        Assert.Contains("CREATE INDEX ix_procesamientos_internos_mensaje_estado_fecha", sql);
        Assert.Contains("CREATE INDEX ix_envios_mensaje_estado_fecha", sql);
    }

    private static string LeerTablasSql()
    {
        DirectoryInfo? directorio = new(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            string ruta = Path.Combine(directorio.FullName, "Datos", "Sql", "tablas.sql");

            if (File.Exists(ruta))
            {
                return File.ReadAllText(ruta);
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException("No se encontro Datos/Sql/tablas.sql.");
    }
}
