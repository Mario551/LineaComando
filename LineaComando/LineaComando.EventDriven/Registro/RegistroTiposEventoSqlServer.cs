using System.Collections.Concurrent;
using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.Cola.BaseDatos;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Registro
{
    public class RegistroTiposEventoSqlServer : IRegistroTiposEvento
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;
        private ConcurrentDictionary<string, TipoEvento> _tiposEventosRegistrados;

        public IDictionary<string, TipoEvento> TiposEventosRegistrados => _tiposEventosRegistrados;

        public RegistroTiposEventoSqlServer(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        public RegistroTiposEventoSqlServer(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.SqlServer(esquema);
            _tiposEventosRegistrados = new ConcurrentDictionary<string, TipoEvento>();
        }

        public async Task<int> RegistrarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default)
        {
            _tiposEventosRegistrados.TryAdd(tipoEvento.Codigo, tipoEvento);

            string sql = $@"
                DECLARE @ResultId INT;

                SELECT @ResultId = id FROM {_nombres.TiposEvento} WHERE codigo = @Codigo;

                IF @ResultId IS NULL
                BEGIN
                    INSERT INTO {_nombres.TiposEvento} (
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
            string sql = $@"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                FROM {_nombres.TiposEvento}
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
            string sql = $@"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                FROM {_nombres.TiposEvento}
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
            string sql = $@"
                SELECT
                    id,
                    codigo,
                    nombre,
                    descripcion,
                    activo,
                    creado_en
                FROM {_nombres.TiposEvento}
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
            string sql = $@"
                UPDATE {_nombres.TiposEvento}
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
            string sql = $@"
                UPDATE {_nombres.TiposEvento}
                SET activo = 0
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
