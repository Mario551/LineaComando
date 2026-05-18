using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;

namespace EventDrivenTest;

public class DatabaseFixtureEventDrivenSqlServer : IAsyncLifetime
{
    public string Esquema { get; } = $"test_{Guid.NewGuid():N}";

    public string ConnectionString { get; }

    public DatabaseFixtureEventDrivenSqlServer()
    {
        ConnectionString = Environment.GetEnvironmentVariable("LINEA_COMANDOS_CONEXION_SQLSERVER")
            ?? throw new InvalidOperationException(
                "La variable de entorno LINEA_COMANDOS_CONEXION_SQLSERVER no está configurada");
    }

    public async Task InitializeAsync()
    {
        var inicializadorCola = new InicializadorEsquemaSqlServer(ConnectionString, Esquema);
        await inicializadorCola.InicializarAsync();

        var inicializadorEventDriven = new InicializadorEsquemaEventDrivenSqlServer(ConnectionString, Esquema);
        await inicializadorEventDriven.InicializarAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}

[CollectionDefinition("DatabaseEventDrivenSqlServer")]
public class DatabaseEventDrivenCollectionSqlServer : ICollectionFixture<DatabaseFixtureEventDrivenSqlServer>
{
}
