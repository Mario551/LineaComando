using Dapper;
using Npgsql;

namespace PER.Comandos.LineaComandos.EventDriven.Esquema
{
    /// <summary>
    /// Servicio para inicializar el esquema de base de datos del sistema event-driven.
    /// Crea las tablas y funciones necesarias si no existen.
    /// IMPORTANTE: Requiere que el esquema de LineaComando.Cola esté inicializado primero
    /// (tabla per_comandos_registrados).
    /// </summary>
    public class InicializadorEsquemaEventDriven
    {
        private readonly string _connectionString;

        public InicializadorEsquemaEventDriven(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        /// <summary>
        /// Inicializa el esquema de base de datos.
        /// Crea las tablas, índices y funciones si no existen.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Si la tabla per_comandos_registrados no existe (dependencia de LineaComando.Cola).
        /// </exception>
        public async Task InicializarAsync(CancellationToken token = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            // Verificar dependencia de LineaComando.Cola
            if (!await TablaExisteAsync(connection, "per_comandos_registrados"))
            {
                throw new InvalidOperationException(
                    "La tabla 'per_comandos_registrados' no existe. " +
                    "Debe inicializar el esquema de LineaComando.Cola primero.");
            }

            await CrearTablaTiposEventoAsync(connection);
            await CrearTablaManejadoresEventoAsync(connection);
            await CrearTablaDisparadoresManejadorAsync(connection);
            await CrearTablaEventosOutboxAsync(connection);
            await CrearFuncionObtenerEventosPendientesAsync(connection);
        }

        /// <summary>
        /// Verifica si el esquema está inicializado.
        /// </summary>
        public async Task<bool> EsquemaExisteAsync(CancellationToken token = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var existeTiposEvento = await TablaExisteAsync(connection, "per_tipos_evento");
            var existeManejadores = await TablaExisteAsync(connection, "per_manejadores_evento");
            var existeDisparadores = await TablaExisteAsync(connection, "per_disparadores_manejador");
            var existeOutbox = await TablaExisteAsync(connection, "per_eventos_outbox");

            return existeTiposEvento && existeManejadores && existeDisparadores && existeOutbox;
        }

        /// <summary>
        /// Verifica si las dependencias (esquema de Cola) están satisfechas.
        /// </summary>
        public async Task<bool> DependenciasSatisfechasAsync(CancellationToken token = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await TablaExisteAsync(connection, "per_comandos_registrados");
        }

        private static async Task<bool> TablaExisteAsync(NpgsqlConnection connection, string nombreTabla)
        {
            const string sql = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = @NombreTabla
                );";

            return await connection.ExecuteScalarAsync<bool>(sql, new { NombreTabla = nombreTabla });
        }

        private static async Task CrearTablaTiposEventoAsync(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS per_tipos_evento (
                    id SERIAL PRIMARY KEY,
                    codigo VARCHAR(255) NOT NULL UNIQUE,
                    nombre VARCHAR(255) NOT NULL,
                    descripcion TEXT NULL,
                    activo BOOLEAN NOT NULL DEFAULT true,
                    creado_en TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_per_tipos_evento_codigo
                    ON per_tipos_evento(codigo);

                CREATE INDEX IF NOT EXISTS idx_per_tipos_evento_activo
                    ON per_tipos_evento(activo);";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaManejadoresEventoAsync(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS per_manejadores_evento (
                    id SERIAL PRIMARY KEY,
                    codigo VARCHAR(255) NOT NULL UNIQUE,
                    nombre VARCHAR(255) NOT NULL,
                    descripcion TEXT NULL,
                    id_comando_registrado INTEGER NOT NULL,
                    ruta_comando VARCHAR(2048) NOT NULL,
                    argumentos_comando TEXT NULL,
                    activo BOOLEAN NOT NULL DEFAULT true,
                    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),

                    CONSTRAINT fk_manejador_comando
                        FOREIGN KEY (id_comando_registrado)
                        REFERENCES per_comandos_registrados(id)
                        ON DELETE NO ACTION
                );

                CREATE INDEX IF NOT EXISTS idx_per_manejadores_evento_codigo
                    ON per_manejadores_evento(codigo);

                CREATE INDEX IF NOT EXISTS idx_per_manejadores_evento_activo
                    ON per_manejadores_evento(activo);

                CREATE INDEX IF NOT EXISTS idx_per_manejadores_evento_comando
                    ON per_manejadores_evento(id_comando_registrado);";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaDisparadoresManejadorAsync(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS per_disparadores_manejador (
                    id SERIAL PRIMARY KEY,
                    manejador_evento_id INTEGER NOT NULL,
                    modo_disparo VARCHAR(50) NOT NULL DEFAULT 'Evento',
                    tipo_evento_id INTEGER NULL,
                    expresion VARCHAR(255) NULL,
                    activo BOOLEAN NOT NULL DEFAULT true,
                    prioridad INTEGER NOT NULL DEFAULT 0,
                    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),

                    CONSTRAINT fk_disparador_manejador
                        FOREIGN KEY (manejador_evento_id)
                        REFERENCES per_manejadores_evento(id)
                        ON DELETE CASCADE,

                    CONSTRAINT fk_disparador_tipo_evento
                        FOREIGN KEY (tipo_evento_id)
                        REFERENCES per_tipos_evento(id)
                        ON DELETE CASCADE,

                    CONSTRAINT chk_modo_disparo
                        CHECK (modo_disparo IN ('Evento', 'Programado')),

                    CONSTRAINT chk_disparador_valido
                        CHECK (
                            (modo_disparo = 'Evento' AND tipo_evento_id IS NOT NULL) OR
                            (modo_disparo = 'Programado' AND expresion IS NOT NULL)
                        )
                );

                CREATE INDEX IF NOT EXISTS idx_per_disparadores_manejador_evento_id
                    ON per_disparadores_manejador(manejador_evento_id);

                CREATE INDEX IF NOT EXISTS idx_disparadores_tipo_evento
                    ON per_disparadores_manejador(tipo_evento_id)
                    WHERE tipo_evento_id IS NOT NULL;

                CREATE INDEX IF NOT EXISTS idx_disparadores_modo
                    ON per_disparadores_manejador(modo_disparo, activo);

                CREATE INDEX IF NOT EXISTS idx_disparadores_programados
                    ON per_disparadores_manejador(modo_disparo, activo, expresion)
                    WHERE modo_disparo = 'Programado';
                    
                CREATE UNIQUE INDEX IF NOT EXISTS idx_manejador_tipo 
                    ON per_disparadores_manejador (manejador_evento_id, COALESCE(tipo_evento_id, -1));
                    
                CREATE UNIQUE INDEX IF NOT EXISTS idx_manejador_expr
                    ON per_disparadores_manejador (manejador_evento_id, COALESCE(expresion, ''));";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaEventosOutboxAsync(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS per_eventos_outbox (
                    id BIGSERIAL PRIMARY KEY,
                    codigo_tipo_evento VARCHAR(255) NOT NULL,
                    agregado_id BIGINT NULL,
                    datos_evento JSONB NOT NULL,
                    metadatos JSONB NULL,
                    creado_en TIMESTAMP NOT NULL DEFAULT NOW(),
                    procesado_en TIMESTAMP NULL
                );

                CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_tipo
                    ON per_eventos_outbox(codigo_tipo_evento);

                CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_procesado
                    ON per_eventos_outbox(procesado_en)
                    WHERE procesado_en IS NULL;

                CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_creado
                    ON per_eventos_outbox(creado_en);

                CREATE INDEX IF NOT EXISTS idx_per_eventos_outbox_pendientes
                    ON per_eventos_outbox(codigo_tipo_evento, creado_en)
                    WHERE procesado_en IS NULL;";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearFuncionObtenerEventosPendientesAsync(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE OR REPLACE FUNCTION obtener_eventos_pendientes(
                    p_tamanio_lote INTEGER DEFAULT 50
                )
                RETURNS TABLE (
                    id BIGINT,
                    codigo_tipo_evento VARCHAR(255),
                    agregado_id BIGINT,
                    datos_evento JSONB,
                    metadatos JSONB,
                    creado_en TIMESTAMP,
                    procesado_en TIMESTAMP
                )
                AS $$
                BEGIN
                    RETURN QUERY
                    SELECT
                        e.id,
                        e.codigo_tipo_evento,
                        e.agregado_id,
                        e.datos_evento,
                        e.metadatos,
                        e.creado_en,
                        e.procesado_en
                    FROM per_eventos_outbox e
                    WHERE e.procesado_en IS NULL
                    ORDER BY e.creado_en
                    LIMIT p_tamanio_lote;
                END;
                $$ LANGUAGE plpgsql;";

            await connection.ExecuteAsync(sql);
        }
    }
}
