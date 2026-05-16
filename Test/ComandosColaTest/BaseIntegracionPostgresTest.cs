using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace ComandosColaTest
{
    public abstract class BaseIntegracionPostgresTest : IAsyncLifetime
    {
        protected readonly string ConnectionString;
        protected readonly string Esquema;
        protected readonly NombresBaseDatos Nombres;

        protected abstract string PrefijoTest { get; }

        protected BaseIntegracionPostgresTest(DatabaseFixture fixture)
        {
            ConnectionString = fixture.ConnectionString;
            Esquema = fixture.Esquema;
            Nombres = NombresBaseDatos.Postgres(Esquema);
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
                $"DELETE FROM {Nombres.ColaComandos} WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.ComandosRegistrados} WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
        }

        protected NpgsqlConnection CrearConexion()
        {
            return new NpgsqlConnection(ConnectionString);
        }
    }
}
