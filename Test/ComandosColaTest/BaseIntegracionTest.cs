using Dapper;
using Npgsql;

namespace ComandosColaTest
{
    public abstract class BaseIntegracionTest : IAsyncLifetime
    {
        protected readonly string ConnectionString;

        protected abstract string PrefijoTest { get; }

        protected BaseIntegracionTest(DatabaseFixture fixture)
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
                "DELETE FROM cola_comandos WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
            await connection.ExecuteAsync(
                "DELETE FROM comandos_registrados WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
        }

        protected NpgsqlConnection CrearConexion()
        {
            return new NpgsqlConnection(ConnectionString);
        }
    }
}
