using System.Collections.Concurrent;
using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Registro
{
    public class RegistroTiposEventoSqlServer : IRegistroTiposEvento
    {
        private readonly string _connectionString;
        private ConcurrentDictionary<string, TipoEvento> _tiposEventosRegistrados;

        public IDictionary<string, TipoEvento> TiposEventosRegistrados => _tiposEventosRegistrados;

        public RegistroTiposEventoSqlServer(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _tiposEventosRegistrados = new ConcurrentDictionary<string, TipoEvento>();
        }

        public async Task<int> RegistrarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            _tiposEventosRegistrados.TryAdd(tipoEvento.Codigo, tipoEvento);

            const string sql = @"
                DECLARE @ResultId INT;

                SELECT @ResultId = id FROM per_tipos_evento WHERE codigo = @Codigo;

                IF @ResultId IS NULL
                BEGIN
                    INSERT INTO per_tipos_evento (
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
                    );
                    
                    SET @ResultId = SCOPE_IDENTITY();
                END

                SELECT @ResultId;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            int id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    tipoEvento.Codigo,
                    tipoEvento.Nombre,
                    tipoEvento.Descripcion,
                    Activo = tipoEvento.Activo ? 1 : 0,
                    tipoEvento.CreadoEn
                });

            tipoEvento.Id = id;
            return id;
        }

        public async Task<TipoEvento?> ObtenerTipoEventoPorCodigoAsync(string codigo, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                FROM per_tipos_evento
                WHERE codigo = @Codigo;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultado = await connection.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { Codigo = codigo });
            
            if (resultado != null)
            {
                resultado.Activo = resultado.Activo;
            }
            
            return resultado;
        }

        public async Task<TipoEvento?> ObtenerTipoEventoPorIdAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                FROM per_tipos_evento
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultado = await connection.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { Id = id });
            
            if (resultado != null)
            {
                resultado.Activo = resultado.Activo;
            }
            
            return resultado;
        }

        public async Task<IEnumerable<TipoEvento>> ObtenerTiposEventosActivosAsync(CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                FROM per_tipos_evento
                WHERE activo = 1
                ORDER BY codigo;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var resultados = await connection.QueryAsync<TipoEvento>(sql);
            
            foreach (var tipo in resultados)
            {
                tipo.Activo = tipo.Activo;
            }
            
            return resultados;
        }

        public async Task ActualizarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_tipos_evento
                SET
                    codigo = @Codigo,
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    activo = @Activo
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new
            {
                tipoEvento.Id,
                tipoEvento.Codigo,
                tipoEvento.Nombre,
                tipoEvento.Descripcion,
                Activo = tipoEvento.Activo ? 1 : 0
            });
        }

        public async Task DesactivarTipoEventoAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_tipos_evento
                SET activo = 0
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
