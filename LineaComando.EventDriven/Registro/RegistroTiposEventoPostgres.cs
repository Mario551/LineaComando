using System.Collections.Concurrent;
using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.BaseDatos;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Registro
{
    public class RegistroTiposEventoPostgres : IRegistroTiposEvento
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;
        private ConcurrentDictionary<string, TipoEvento> _tiposEventosRegistrados;

        public IDictionary<string, TipoEvento> TiposEventosRegistrados => _tiposEventosRegistrados;

        public RegistroTiposEventoPostgres(string connectionString)
            : this(connectionString, "public")
        {
        }

        public RegistroTiposEventoPostgres(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.Postgres(esquema);
            _tiposEventosRegistrados = new ConcurrentDictionary<string, TipoEvento>();
        }

        public async Task<int> RegistrarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            _tiposEventosRegistrados.TryAdd(tipoEvento.Codigo, tipoEvento);

            string sql = $@"
                INSERT INTO {_nombres.TiposEvento} AS te (
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                )
                VALUES (
                    @Codigo,
                    @Nombre,
                    @Descripcion,
                    @Activo,
                    @CreadoEn
                )
                ON CONFLICT (codigo)
                DO UPDATE SET
                    codigo = EXCLUDED.codigo
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            int id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    tipoEvento.Codigo,
                    tipoEvento.Nombre,
                    tipoEvento.Descripcion,
                    tipoEvento.Activo,
                    tipoEvento.CreadoEn
                });

            tipoEvento.Id = id;
            return id;
        }

        public async Task<TipoEvento?> ObtenerTipoEventoPorCodigoAsync(string codigo, CancellationToken token = default)
        {
            string sql = $@"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM {_nombres.TiposEvento}
                WHERE codigo = @Codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { Codigo = codigo });
        }

        public async Task<TipoEvento?> ObtenerTipoEventoPorIdAsync(int id, CancellationToken token = default)
        {
            string sql = $@"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM {_nombres.TiposEvento}
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { Id = id });
        }

        public async Task<IEnumerable<TipoEvento>> ObtenerTiposEventosActivosAsync(CancellationToken token = default)
        {
            string sql = $@"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM {_nombres.TiposEvento}
                WHERE activo = true
                ORDER BY codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<TipoEvento>(sql);
        }

        public async Task ActualizarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.TiposEvento}
                SET
                    codigo = @Codigo,
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    activo = @Activo
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, tipoEvento);
        }

        public async Task DesactivarTipoEventoAsync(int id, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.TiposEvento}
                SET activo = false
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
