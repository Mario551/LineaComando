using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace ComandosColaTest
{
    public abstract class BaseIntegracionSqlServerTest : IAsyncLifetime
    {
        protected readonly string ConnectionString;
        protected readonly string Esquema;
        protected readonly NombresBaseDatos Nombres;

        protected abstract string PrefijoTest { get; }

        protected BaseIntegracionSqlServerTest(DatabaseFixtureSqlServer fixture)
        {
            ConnectionString = fixture.ConnectionString;
            Esquema = fixture.Esquema;
            Nombres = NombresBaseDatos.SqlServer(Esquema);
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

        private async Task LimpiarDatosDelTestAsync()
        {
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.ColaComandos} WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.ComandosRegistrados} WHERE ruta_comando LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });
        }

        protected SqlConnection CrearConexion()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}
