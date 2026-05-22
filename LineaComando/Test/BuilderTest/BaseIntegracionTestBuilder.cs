using Dapper;
using Npgsql;
using BuilderTest.BuilderComandoTest;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace BuilderTest;

public abstract class BaseIntegracionTestBuilder : IAsyncLifetime
{
    protected readonly string ConnectionString;
    protected readonly string Esquema;
    protected readonly NombresBaseDatos Nombres;

    protected abstract string PrefijoTest { get; }

    protected BaseIntegracionTestBuilder(DatabaseFixture fixture)
    {
        ConnectionString = fixture.ConnectionString;
        Esquema = fixture.Esquema;
        Nombres = NombresBaseDatos.Postgres(Esquema);
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public virtual async ValueTask InitializeAsync()
    {
        await LimpiarDatosDelTestAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        await LimpiarDatosDelTestAsync();
    }

    protected virtual async Task LimpiarDatosDelTestAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            $"DELETE FROM {Nombres.ComandosRegistrados} WHERE ruta_comando LIKE @Prefijo;",
            new { Prefijo = PrefijoTest + "%" });
    }

    protected NpgsqlConnection CrearConexion()
    {
        return new NpgsqlConnection(ConnectionString);
    }
}
