using Dapper;
using Microsoft.Data.SqlClient;

namespace ComandosColaTest
{
    public abstract class BaseIntegracionSqlServerTest : IAsyncLifetime
    {
        protected readonly string ConnectionString;

        protected abstract string PrefijoTest { get; }

        protected BaseIntegracionSqlServerTest(DatabaseFixtureSqlServer fixture)
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
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM per_cola_comandos WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
            await connection.ExecuteAsync(
                "DELETE FROM per_comandos_registrados WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
        }

        protected SqlConnection CrearConexion()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
