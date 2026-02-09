using Dapper;
using Npgsql;
using BuilderTest.BuilderComandoTest;

namespace BuilderTest;

public abstract class BaseIntegracionTestBuilder : IAsyncLifetime
{
    protected readonly string ConnectionString;

    protected abstract string PrefijoTest { get; }

    protected BaseIntegracionTestBuilder(DatabaseFixture fixture)
    {
        ConnectionString = fixture.ConnectionString;
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public virtual async Task InitializeAsync()
    {
        await LimpiarDatosDelTestAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await LimpiarDatosDelTestAsync();
    }

    private async Task LimpiarDatosDelTestAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM per_comandos_registrados WHERE ruta_comando LIKE @Prefijo;",
            new { Prefijo = PrefijoTest + "%" });
    }

    protected NpgsqlConnection CrearConexion()
    {
        return new NpgsqlConnection(ConnectionString);
    }
}
