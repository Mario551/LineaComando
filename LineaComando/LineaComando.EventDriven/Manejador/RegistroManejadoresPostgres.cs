using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.BaseDatos;
using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Manejador
{
    public class RegistroManejadoresPostgres : IRegistroManejadores
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public RegistroManejadoresPostgres(string connectionString)
            : this(connectionString, "public")
        {
        }

        public RegistroManejadoresPostgres(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.Postgres(esquema);
        }

        public async Task<int> RegistrarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default)
        {
            string sql = $@"
                INSERT INTO {_nombres.ManejadoresEvento} AS m (
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
                    codigo = EXCLUDED.codigo
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            int id = await connection.ExecuteScalarAsync<int>(
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
            
            manejador.Id = id;
            return id;
        }

        public async Task<ManejadorEvento?> ObtenerManejadorPorIdAsync(int id, CancellationToken token = default)
        {
            string sql = $@"
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
                FROM {_nombres.ManejadoresEvento}
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<ManejadorEvento>(sql, new { Id = id });
        }

        public async Task<ManejadorEvento?> ObtenerManejadorPorCodigoAsync(string codigo, CancellationToken token = default)
        {
            string sql = $@"
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
                FROM {_nombres.ManejadoresEvento}
                WHERE codigo = @Codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QuerySingleOrDefaultAsync<ManejadorEvento>(sql, new { Codigo = codigo });
        }

        public async Task<IEnumerable<ManejadorEvento>> ObtenerManejadoresActivosAsync(CancellationToken token = default)
        {
            string sql = $@"
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
                FROM {_nombres.ManejadoresEvento}
                WHERE activo = true
                ORDER BY codigo;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<ManejadorEvento>(sql);
        }

        public async Task ActualizarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.ManejadoresEvento}
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
            string sql = $@"
                UPDATE {_nombres.ManejadoresEvento}
                SET activo = false
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<int> RegistrarDisparadorAsync(DisparadorManejador disparador, CancellationToken token = default)
        {
            string sql = $@"
                INSERT INTO {_nombres.DisparadoresManejador} AS d (
                    manejador_evento_id,
                    codigo,
                    modo_disparo,
                    tipo_evento_id,
                    expresion,
                    activo,
                    prioridad,
                    creado_en
                )
                VALUES (
                    @ManejadorEventoId,
                    @Codigo,
                    @ModoDisparo,
                    @TipoEventoId,
                    @Expresion,
                    @Activo,
                    @Prioridad,
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
                    disparador.ManejadorEventoId,
                    disparador.Codigo,
                    disparador.ModoDisparo,
                    disparador.TipoEventoId,
                    disparador.Expresion,
                    disparador.Activo,
                    disparador.Prioridad,
                    disparador.CreadoEn
                });

            return id;
        }

        public async Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresParaEventoAsync(
            string tipoEvento,
            CancellationToken token = default)
        {
            string sql = $@"
                SELECT
                    d.id as Id,
                    d.manejador_evento_id as IDManejador,
                    m.id_comando_registrado as IdComandoRegistrado,
                    m.ruta_comando as RutaComando,
                    m.argumentos_comando as ArgumentosComando,
                    d.modo_disparo as ModoDisparo,
                    te.codigo as CodigoTipoEvento,
                    d.codigo as Codigo,
                    d.expresion as Expresion,
                    d.activo as Activo,
                    d.prioridad as Prioridad,
                    d.creado_en as FechaCreacion
                FROM {_nombres.DisparadoresManejador} d
                INNER JOIN {_nombres.ManejadoresEvento} m ON d.manejador_evento_id = m.id
                INNER JOIN {_nombres.TiposEvento} te ON d.tipo_evento_id = te.id
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
            string sql = $@"
                SELECT
                    d.id as Id,
                    d.manejador_evento_id as IDManejador,
                    m.id_comando_registrado as IdComandoRegistrado,
                    m.ruta_comando as RutaComando,
                    m.argumentos_comando as ArgumentosComando,
                    d.modo_disparo as ModoDisparo,
                    d.codigo as Codigo,
                    d.expresion as Expresion,
                    d.activo as Activo,
                    d.prioridad as Prioridad,
                    d.creado_en as FechaCreacion,
                    d.ultima_ejecucion as UltimaEjecucion
                FROM {_nombres.DisparadoresManejador} d
                INNER JOIN {_nombres.ManejadoresEvento} m ON d.manejador_evento_id = m.id
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
            string sql = $@"
                UPDATE {_nombres.DisparadoresManejador}
                SET
                    modo_disparo = @ModoDisparo,
                    expresion = @Expresion,
                    activo = @Activo,
                    prioridad = @Prioridad
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, configuracion);
        }


        public async Task ActualizarUltimaEjecucionAsync(int disparadorId, DateTime ultimaEjecucion, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.DisparadoresManejador}
                SET ultima_ejecucion = @UltimaEjecucion
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = disparadorId, UltimaEjecucion = ultimaEjecucion });
        }
    }
}
