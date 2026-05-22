using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace PER.Comandos.LineaComandos.Cola.Esquema
{
    /// <summary>
    /// Servicio para inicializar el esquema de base de datos de la cola de comandos.
    /// Crea las tablas y funciones necesarias si no existen.
    /// </summary>
    public class InicializadorEsquemaPostgres
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public InicializadorEsquemaPostgres(string connectionString)
            : this(connectionString, "public")
        {
        }

        public InicializadorEsquemaPostgres(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.Postgres(esquema);
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        /// <summary>
        /// Inicializa el esquema de base de datos.
        /// Crea las tablas, índices y funciones si no existen.
        /// </summary>
        public async Task InicializarAsync(CancellationToken token = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await CrearEsquemaAsync(connection);
            await CrearTablaComandosRegistradosAsync(connection);
            await CrearTablaEstadosColaComandosAsync(connection);
            await CrearTablaColaComandosAsync(connection);
            await CrearTablaResultadosColaComandosAsync(connection);
            await CrearFuncionObtenerComandosPendientesAsync(connection);
            await CrearFuncionMarcarComandosProcesandoAsync(connection);
            await CrearProcedimientoActualizarFechaLeidoAsync(connection);
        }

        /// <summary>
        /// Verifica si el esquema está inicializado.
        /// </summary>
        public async Task<bool> EsquemaExisteAsync(CancellationToken token = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var existeComandosRegistrados = await TablaExisteAsync(connection, "per_comandos_registrados");
            var existeEstadosColaComandos = await TablaExisteAsync(connection, "per_cola_comandos_estados");
            var existeColaComandos = await TablaExisteAsync(connection, "per_cola_comandos");
            var existeResultadosColaComandos = await TablaExisteAsync(connection, "per_cola_comandos_resultados");

            return existeComandosRegistrados && existeEstadosColaComandos && existeColaComandos && existeResultadosColaComandos;
        }

        private async Task<bool> TablaExisteAsync(NpgsqlConnection connection, string nombreTabla)
        {
            const string sql = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = @Esquema
                    AND table_name = @NombreTabla
                );";

            return await connection.ExecuteScalarAsync<bool>(sql, new { _nombres.Esquema, NombreTabla = nombreTabla });
        }

        private async Task CrearEsquemaAsync(NpgsqlConnection connection)
        {
            string sql = $"CREATE SCHEMA IF NOT EXISTS {_nombres.EsquemaSql};";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaComandosRegistradosAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE TABLE IF NOT EXISTS {_nombres.ComandosRegistrados} (
                    id SERIAL PRIMARY KEY,
                    ruta_comando VARCHAR(2048) NOT NULL UNIQUE,
                    descripcion TEXT NULL,
                    activo BOOLEAN NOT NULL DEFAULT true,
                    creado_en TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    actualizado_en TIMESTAMP WITHOUT TIME ZONE NULL
                );

                CREATE INDEX IF NOT EXISTS idx_per_comandos_registrados_ruta
                    ON {_nombres.ComandosRegistrados}(ruta_comando);

                CREATE INDEX IF NOT EXISTS idx_per_comandos_registrados_activo
                    ON {_nombres.ComandosRegistrados}(activo);";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaEstadosColaComandosAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE TABLE IF NOT EXISTS {_nombres.ColaComandosEstados} (
                    estado VARCHAR(50) PRIMARY KEY,
                    descripcion VARCHAR(200) NOT NULL
                );

                INSERT INTO {_nombres.ColaComandosEstados} (estado, descripcion)
                VALUES
                    ('pendiente', 'Comando registrado y pendiente de tomar.'),
                    ('procesando', 'Comando tomado por un worker.'),
                    ('completado', 'Comando ejecutado correctamente.'),
                    ('fallido', 'Comando terminado con error.')
                ON CONFLICT (estado) DO UPDATE
                SET descripcion = EXCLUDED.descripcion;";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaColaComandosAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE TABLE IF NOT EXISTS {_nombres.ColaComandos} (
                    id BIGSERIAL PRIMARY KEY,
                    id_comando_registrado INTEGER NOT NULL,
                    ruta_comando VARCHAR(2048) NOT NULL,
                    argumentos TEXT NULL,
                    datos_comando JSONB NULL,
                    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    fecha_leido TIMESTAMP WITHOUT TIME ZONE NULL,
                    fecha_ejecucion TIMESTAMP WITHOUT TIME ZONE NULL,
                    estado VARCHAR(50) NOT NULL DEFAULT 'pendiente',
                    mensaje_error TEXT NULL,
                    duracion_ms BIGINT NULL,
                    intentos INTEGER NOT NULL DEFAULT 0,

                    CONSTRAINT fk_per_cola_comandos_comando_registrado
                        FOREIGN KEY (id_comando_registrado)
                        REFERENCES {_nombres.ComandosRegistrados}(id)
                        ON DELETE NO ACTION,

                    CONSTRAINT fk_per_cola_comandos_estado
                        FOREIGN KEY (estado)
                        REFERENCES {_nombres.ColaComandosEstados}(estado)
                        ON DELETE NO ACTION
                );

                CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_estado
                    ON {_nombres.ColaComandos}(estado);

                CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_fecha_creacion
                    ON {_nombres.ColaComandos}(fecha_creacion);

                CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_fecha_leido
                    ON {_nombres.ColaComandos}(fecha_leido)
                    WHERE fecha_leido IS NOT NULL;

                CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_pendientes
                    ON {_nombres.ColaComandos}(id, fecha_creacion)
                    WHERE estado = 'pendiente' AND fecha_leido IS NULL;";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaResultadosColaComandosAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE TABLE IF NOT EXISTS {_nombres.ColaComandosResultados} (
                    comando_id BIGINT PRIMARY KEY,
                    tipo VARCHAR(200) NOT NULL,
                    version_resultado INTEGER NOT NULL,
                    formato VARCHAR(100) NOT NULL,
                    payload TEXT NULL,
                    ruta_payload TEXT NULL,
                    creado_en TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),

                    CONSTRAINT fk_per_cola_comandos_resultados_comando
                        FOREIGN KEY (comando_id)
                        REFERENCES {_nombres.ColaComandos}(id)
                        ON DELETE CASCADE,

                    CONSTRAINT ck_per_cola_comandos_resultados_payload_o_ruta
                        CHECK (
                            (payload IS NOT NULL AND ruta_payload IS NULL)
                            OR
                            (payload IS NULL AND ruta_payload IS NOT NULL)
                        )
                );

                CREATE INDEX IF NOT EXISTS idx_per_cola_comandos_resultados_tipo_version
                    ON {_nombres.ColaComandosResultados}(tipo, version_resultado);";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearFuncionObtenerComandosPendientesAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE OR REPLACE FUNCTION {_nombres.ObtenerComandosPendientes}(
                    p_tamanio_lote INTEGER DEFAULT 50,
                    p_timeout_milisegundos INTEGER DEFAULT 300000
                )
                RETURNS TABLE (
                    id BIGINT,
                    id_comando_registrado INTEGER,
                    ruta_comando VARCHAR(2048),
                    argumentos TEXT,
                    datos_comando JSONB,
                    fecha_creacion TIMESTAMP WITHOUT TIME ZONE,
                    fecha_leido TIMESTAMP WITHOUT TIME ZONE,
                    fecha_ejecucion TIMESTAMP WITHOUT TIME ZONE,
                    estado VARCHAR(50),
                    mensaje_error TEXT,
                    duracion_ms BIGINT,
                    intentos INTEGER
                )
                AS $$
                DECLARE
                    v_timeout_timestamp TIMESTAMP WITHOUT TIME ZONE;
                BEGIN
                    v_timeout_timestamp := NOW() - (p_timeout_milisegundos || ' milliseconds')::INTERVAL;

                    RETURN QUERY
                    SELECT c.*
                    FROM {_nombres.ColaComandos} c
                    WHERE (
                        (c.fecha_leido IS NULL AND c.estado = 'pendiente')
                        OR
                        (c.estado = 'procesando' AND c.fecha_leido < v_timeout_timestamp)
                    )
                    ORDER BY c.id
                    LIMIT p_tamanio_lote;
                END;
                $$ LANGUAGE plpgsql;";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearFuncionMarcarComandosProcesandoAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE OR REPLACE FUNCTION {_nombres.MarcarComandosProcesando}(
                    p_ids BIGINT[]
                )
                RETURNS TABLE (
                    id BIGINT,
                    id_comando_registrado INTEGER,
                    ruta_comando VARCHAR(2048),
                    argumentos TEXT,
                    datos_comando JSONB,
                    fecha_creacion TIMESTAMP WITHOUT TIME ZONE,
                    fecha_leido TIMESTAMP WITHOUT TIME ZONE,
                    fecha_ejecucion TIMESTAMP WITHOUT TIME ZONE,
                    estado VARCHAR(50),
                    mensaje_error TEXT,
                    duracion_ms BIGINT,
                    intentos INTEGER
                )
                AS $$
                BEGIN
                    UPDATE {_nombres.ColaComandos} c
                    SET fecha_leido = NOW(),
                        estado = 'procesando'
                    WHERE c.id = ANY(p_ids);

                    RETURN QUERY SELECT c.* FROM {_nombres.ColaComandos} c WHERE c.id = ANY(p_ids);
                END;
                $$ LANGUAGE plpgsql;";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearProcedimientoActualizarFechaLeidoAsync(NpgsqlConnection connection)
        {
            string sql = $@"
                CREATE OR REPLACE PROCEDURE {_nombres.ActualizarFechaLeido}(
                    p_ids BIGINT[]
                )
                AS $$
                BEGIN
                    UPDATE {_nombres.ColaComandos}
                    SET fecha_leido = NOW()
                    WHERE id = ANY(p_ids)
                    AND fecha_leido IS NULL;
                END;
                $$ LANGUAGE plpgsql;";

            await connection.ExecuteAsync(sql);
        }
    }
}
