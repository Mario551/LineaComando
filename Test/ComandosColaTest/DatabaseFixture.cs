using PER.Comandos.LineaComandos.Cola.Esquema;

namespace ComandosColaTest;

public class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("LINEA_COMANDOS_CONEXION_POSTGRESQL")
            ?? throw new InvalidOperationException(
                "La variable de entorno LINEA_COMANDOS_CONEXION_POSTGRESQL no está configurada");
    }

    public async Task InitializeAsync()
    {
        var inicializador = new InicializadorEsquema(ConnectionString);
        await inicializador.InicializarAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
