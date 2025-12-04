using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Registro
{
    public class RegistroTiposEvento : IRegistroTiposEvento
    {
        private readonly string _connectionString;

        public RegistroTiposEvento(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<int> RegistrarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            const string sql = @"
                INSERT INTO tipos_evento (
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
                    nombre = EXCLUDED.nombre,
                    descripcion = EXCLUDED.descripcion,
                    activo = EXCLUDED.activo
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    tipoEvento.Codigo,
                    tipoEvento.Nombre,
                    tipoEvento.Descripcion,
                    tipoEvento.Activo,
                    tipoEvento.CreadoEn
                });

            return id;
        }

        public async Task<TipoEvento?> ObtenerTipoEventoPorCodigoAsync(string codigo, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM tipos_evento
                WHERE codigo = @Codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { Codigo = codigo });
        }

        public async Task<TipoEvento?> ObtenerTipoEventoPorIdAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM tipos_evento
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { Id = id });
        }

        public async Task<IEnumerable<TipoEvento>> ObtenerTiposEventosActivosAsync(CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM tipos_evento
                WHERE activo = true
                ORDER BY codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<TipoEvento>(sql);
        }

        public async Task ActualizarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE tipos_evento
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
            const string sql = @"
                UPDATE tipos_evento
                SET activo = false
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
