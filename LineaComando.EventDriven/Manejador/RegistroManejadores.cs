using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Manejador
{
    public class RegistroManejadores : IRegistroManejadores
    {
        private readonly string _connectionString;

        public RegistroManejadores(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<int> RegistrarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default)
        {
            const string sql = @"
                INSERT INTO manejadores_evento (
                    codigo,
                    nombre,
                    descripcion,
                    id_comando_registrado,
                    ruta_comando,
                    argumentos_comando,
                    activo,
                    creado_en
                )
                VALUES (
                    @Codigo,
                    @Nombre,
                    @Descripcion,
                    @IdComandoRegistrado,
                    @RutaComando,
                    @ArgumentosComando,
                    @Activo,
                    @CreadoEn
                )
                ON CONFLICT (codigo)
                DO UPDATE SET
                    nombre = EXCLUDED.nombre,
                    descripcion = EXCLUDED.descripcion,
                    id_comando_registrado = EXCLUDED.id_comando_registrado,
                    ruta_comando = EXCLUDED.ruta_comando,
                    argumentos_comando = EXCLUDED.argumentos_comando,
                    activo = EXCLUDED.activo
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var id = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    manejador.Codigo,
                    manejador.Nombre,
                    manejador.Descripcion,
                    manejador.IdComandoRegistrado,
                    manejador.RutaComando,
                    manejador.ArgumentosComando,
                    manejador.Activo,
                    manejador.CreadoEn
                });

            return id;
        }

        public async Task<ManejadorEvento?> ObtenerManejadorPorIdAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    id_comando_registrado as IdComandoRegistrado,
                    ruta_comando as RutaComando,
                    argumentos_comando as ArgumentosComando,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM manejadores_evento
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<ManejadorEvento>(sql, new { Id = id });
        }

        public async Task<ManejadorEvento?> ObtenerManejadorPorCodigoAsync(string codigo, CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    id_comando_registrado as IdComandoRegistrado,
                    ruta_comando as RutaComando,
                    argumentos_comando as ArgumentosComando,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM manejadores_evento
                WHERE codigo = @Codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<ManejadorEvento>(sql, new { Codigo = codigo });
        }

        public async Task<IEnumerable<ManejadorEvento>> ObtenerManejadoresActivosAsync(CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo as Codigo,
                    nombre as Nombre,
                    descripcion as Descripcion,
                    id_comando_registrado as IdComandoRegistrado,
                    ruta_comando as RutaComando,
                    argumentos_comando as ArgumentosComando,
                    activo as Activo,
                    creado_en as CreadoEn
                FROM manejadores_evento
                WHERE activo = true
                ORDER BY codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<ManejadorEvento>(sql);
        }

        public async Task ActualizarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE manejadores_evento
                SET
                    codigo = @Codigo,
                    nombre = @Nombre,
                    descripcion = @Descripcion,
                    id_comando_registrado = @IdComandoRegistrado,
                    ruta_comando = @RutaComando,
                    argumentos_comando = @ArgumentosComando,
                    activo = @Activo
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, manejador);
        }

        public async Task DesactivarManejadorAsync(int id, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE manejadores_evento
                SET activo = false
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresParaEventoAsync(
            string tipoEvento,
            CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    d.id as Id,
                    d.manejador_evento_id as IDManejador,
                    m.id_comando_registrado as IdComandoRegistrado,
                    m.ruta_comando as RutaComando,
                    m.argumentos_comando as ArgumentosComando,
                    d.modo_disparo as ModoDisparo,
                    te.codigo as CodigoTipoEvento,
                    d.expresion_cron as ExpresionCron,
                    d.activo as Activo,
                    d.prioridad as Prioridad,
                    d.creado_en as FechaCreacion
                FROM disparadores_manejador d
                INNER JOIN manejadores_evento m ON d.manejador_evento_id = m.id
                INNER JOIN tipos_evento te ON d.tipo_evento_id = te.id
                WHERE te.codigo = @TipoEvento
                    AND d.activo = true
                    AND m.activo = true
                ORDER BY d.prioridad;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<ConfiguracionManejador>(sql, new { TipoEvento = tipoEvento });
        }

        public async Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresProgramadosAsync(
            CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    d.id as Id,
                    d.manejador_evento_id as IDManejador,
                    m.id_comando_registrado as IdComandoRegistrado,
                    m.ruta_comando as RutaComando,
                    m.argumentos_comando as ArgumentosComando,
                    d.modo_disparo as ModoDisparo,
                    d.expresion_cron as ExpresionCron,
                    d.activo as Activo,
                    d.prioridad as Prioridad,
                    d.creado_en as FechaCreacion
                FROM disparadores_manejador d
                INNER JOIN manejadores_evento m ON d.manejador_evento_id = m.id
                WHERE d.modo_disparo = 'Programado'
                    AND d.activo = true
                    AND m.activo = true
                ORDER BY d.prioridad;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<ConfiguracionManejador>(sql);
        }

        public async Task ActualizarConfiguracionAsync(ConfiguracionManejador configuracion, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE disparadores_manejador
                SET
                    modo_disparo = @ModoDisparo,
                    expresion_cron = @ExpresionCron,
                    activo = @Activo,
                    prioridad = @Prioridad
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, configuracion);
        }
    }
}
