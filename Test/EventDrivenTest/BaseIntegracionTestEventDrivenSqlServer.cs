using Dapper;
using Microsoft.Data.SqlClient;

namespace EventDrivenTest
{
    public abstract class BaseIntegracionTestEventDrivenSqlServer : IAsyncLifetime
    {
        protected readonly string ConnectionString;

        protected abstract string PrefijoTest { get; }

        protected BaseIntegracionTestEventDrivenSqlServer(DatabaseFixtureEventDrivenSqlServer fixture)
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
                "DELETE FROM per_eventos_outbox WHERE codigo_tipo_evento LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });

            await connection.ExecuteAsync(
                "DELETE FROM per_disparadores_manejador WHERE manejador_evento_id IN (SELECT id FROM per_manejadores_evento WHERE codigo LIKE @Prefijo);",
                new { Prefijo = PrefijoTest + "%" });

            await connection.ExecuteAsync(
                "DELETE FROM per_manejadores_evento WHERE codigo LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });

            await connection.ExecuteAsync(
                "DELETE FROM per_tipos_evento WHERE codigo LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });

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
