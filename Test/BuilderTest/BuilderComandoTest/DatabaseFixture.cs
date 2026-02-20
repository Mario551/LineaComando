using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;

namespace BuilderTest.BuilderComandoTest;

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
        var inicializadorCola = new InicializadorEsquemaPostgres(ConnectionString);
        await inicializadorCola.InicializarAsync();

        var inicializadorEventDriven = new InicializadorEsquemaEventDriven(ConnectionString);
        await inicializadorEventDriven.InicializarAsync();
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
