using PER.Comandos.LineaComandos.Cola.Esquema;
using PER.Comandos.LineaComandos.EventDriven.Esquema;

namespace ComandosColaTest;

public class DatabaseFixture : IAsyncLifetime
{
    public string Esquema { get; } = $"test_{Guid.NewGuid():N}";

    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("LINEA_COMANDOS_CONEXION_POSTGRESQL")
            ?? throw new InvalidOperationException(
                "La variable de entorno LINEA_COMANDOS_CONEXION_POSTGRESQL no está configurada");
    }

    public async ValueTask InitializeAsync()
    {
        var inicializadorCola = new InicializadorEsquemaPostgres(ConnectionString, Esquema);
        await inicializadorCola.InicializarAsync();

        var inicializadorEventDriven = new InicializadorEsquemaEventDrivenPostgres(ConnectionString, Esquema);
        await inicializadorEventDriven.InicializarAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
