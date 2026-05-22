using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace EventDrivenTest
{
    public abstract class BaseIntegracionTestEventDrivenPostgres : IAsyncLifetime
    {
        protected readonly string ConnectionString;
        protected readonly string Esquema;
        protected readonly NombresBaseDatos Nombres;

        protected abstract string PrefijoTest { get; }

        protected BaseIntegracionTestEventDrivenPostgres(DatabaseFixtureEventDrivenPostgres fixture)
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

        private async Task LimpiarDatosDelTestAsync()
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.EventosOutbox} WHERE codigo_tipo_evento LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });

            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.DisparadoresManejador} WHERE manejador_evento_id IN (SELECT id FROM {Nombres.ManejadoresEvento} WHERE codigo LIKE @Prefijo);",
                new { Prefijo = PrefijoTest + "%" });

            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.ManejadoresEvento} WHERE codigo LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });

            await connection.ExecuteAsync(
                $"DELETE FROM {Nombres.TiposEvento} WHERE codigo LIKE @Prefijo;",
                new { Prefijo = PrefijoTest + "%" });

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
