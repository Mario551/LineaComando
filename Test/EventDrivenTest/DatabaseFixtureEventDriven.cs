using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;

namespace EventDrivenTest;

public class DatabaseFixtureEventDriven : IAsyncLifetime
{
    public string ConnectionString { get; }

    public DatabaseFixtureEventDriven()
    {
        ConnectionString = Environment.GetEnvironmentVariable("LINEA_COMANDOS_CONEXION_POSTGRESQL")
            ?? throw new InvalidOperationException(
                "La variable de entorno LINEA_COMANDOS_CONEXION_POSTGRESQL no está configurada");
    }

    public async Task InitializeAsync()
    {
        var inicializadorCola = new InicializadorEsquema(ConnectionString);
        await inicializadorCola.InicializarAsync();

        var inicializadorEventDriven = new InicializadorEsquemaEventDriven(ConnectionString);
        await inicializadorEventDriven.InicializarAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}

[CollectionDefinition("DatabaseEventDriven")]
public class DatabaseEventDrivenCollection : ICollectionFixture<DatabaseFixtureEventDriven>
{
}
