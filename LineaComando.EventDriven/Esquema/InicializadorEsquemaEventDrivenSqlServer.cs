using Dapper;
using Microsoft.Data.SqlClient;

namespace PER.Comandos.LineaComandos.EventDriven.Esquema
{
    public class InicializadorEsquemaEventDrivenSqlServer
    {
        private readonly string _connectionString;

        public InicializadorEsquemaEventDrivenSqlServer(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public async Task InicializarAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

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

        public async Task<bool> EsquemaExisteAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var existeTiposEvento = await TablaExisteAsync(connection, "per_tipos_evento");
            var existeManejadores = await TablaExisteAsync(connection, "per_manejadores_evento");
            var existeDisparadores = await TablaExisteAsync(connection, "per_disparadores_manejador");
            var existeOutbox = await TablaExisteAsync(connection, "per_eventos_outbox");

            return existeTiposEvento && existeManejadores && existeDisparadores && existeOutbox;
        }

        public async Task<bool> DependenciasSatisfechasAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await TablaExisteAsync(connection, "per_comandos_registrados");
        }

        private static async Task<bool> TablaExisteAsync(SqlConnection connection, string nombreTabla)
        {
            const string sql = @"
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM sys.tables 
                    WHERE name = @NombreTabla
                ) THEN 1 ELSE 0 END AS BIT);";

            return await connection.ExecuteScalarAsync<bool>(sql, new { NombreTabla = nombreTabla });
        }

        private static async Task CrearTablaTiposEventoAsync(SqlConnection connection)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_tipos_evento')
                BEGIN
                    CREATE TABLE per_tipos_evento (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        codigo NVARCHAR(255) NOT NULL UNIQUE,
                        nombre NVARCHAR(255) NOT NULL,
                        descripcion NVARCHAR(2048) NULL,
                        activo INT NOT NULL DEFAULT 1,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE()
                    );

                    CREATE INDEX idx_per_tipos_evento_codigo 
                        ON per_tipos_evento(codigo);

                    CREATE INDEX idx_per_tipos_evento_activo 
                        ON per_tipos_evento(activo);
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaManejadoresEventoAsync(SqlConnection connection)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_manejadores_evento')
                BEGIN
                    CREATE TABLE per_manejadores_evento (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        codigo NVARCHAR(255) NOT NULL UNIQUE,
                        nombre NVARCHAR(255) NOT NULL,
                        descripcion NVARCHAR(2048) NULL,
                        id_comando_registrado INT NOT NULL,
                        ruta_comando NVARCHAR(2048) NOT NULL,
                        argumentos_comando NVARCHAR(2048) NULL,
                        activo INT NOT NULL DEFAULT 1,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),

                        CONSTRAINT fk_manejador_comando
                            FOREIGN KEY (id_comando_registrado)
                            REFERENCES per_comandos_registrados(id)
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX idx_per_manejadores_evento_codigo 
                        ON per_manejadores_evento(codigo);

                    CREATE INDEX idx_per_manejadores_evento_activo 
                        ON per_manejadores_evento(activo);

                    CREATE INDEX idx_per_manejadores_evento_comando 
                        ON per_manejadores_evento(id_comando_registrado);
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaDisparadoresManejadorAsync(SqlConnection connection)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_disparadores_manejador')
                BEGIN
                    CREATE TABLE per_disparadores_manejador (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        codigo NVARCHAR(255) NOT NULL UNIQUE,
                        manejador_evento_id INT NOT NULL,
                        modo_disparo NVARCHAR(50) NOT NULL DEFAULT 'Evento',
                        tipo_evento_id INT NULL,
                        expresion NVARCHAR(255) NULL,
                        activo INT NOT NULL DEFAULT 1,
                        prioridad INT NOT NULL DEFAULT 0,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
                        ultima_ejecucion DATETIME2 NULL,

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

                    CREATE INDEX idx_per_disparadores_manejador_evento_id 
                        ON per_disparadores_manejador(manejador_evento_id);

                    CREATE INDEX idx_disparadores_tipo_evento 
                        ON per_disparadores_manejador(tipo_evento_id) 
                        WHERE tipo_evento_id IS NOT NULL;

                    CREATE INDEX idx_disparadores_modo 
                        ON per_disparadores_manejador(modo_disparo, activo);

                    CREATE INDEX idx_disparadores_programados 
                        ON per_disparadores_manejador(modo_disparo, activo, expresion) 
                        WHERE modo_disparo = 'Programado';
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaEventosOutboxAsync(SqlConnection connection)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_eventos_outbox')
                BEGIN
                    CREATE TABLE per_eventos_outbox (
                        id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        codigo_tipo_evento NVARCHAR(255) NOT NULL,
                        agregado_id BIGINT NULL,
                        datos_evento NVARCHAR(MAX) NOT NULL,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
                        procesado_en DATETIME2 NULL
                    );

                    CREATE INDEX idx_per_eventos_outbox_tipo 
                        ON per_eventos_outbox(codigo_tipo_evento);

                    CREATE INDEX idx_per_eventos_outbox_procesado 
                        ON per_eventos_outbox(procesado_en) 
                        WHERE procesado_en IS NULL;

                    CREATE INDEX idx_per_eventos_outbox_creado 
                        ON per_eventos_outbox(creado_en);

                    CREATE INDEX idx_per_eventos_outbox_pendientes 
                        ON per_eventos_outbox(codigo_tipo_evento, creado_en) 
                        WHERE procesado_en IS NULL;
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearFuncionObtenerEventosPendientesAsync(SqlConnection connection)
        {
            const string sql = @"
                CREATE OR ALTER FUNCTION obtener_eventos_pendientes(
                    @tamanio_lote INT = 50
                )
                RETURNS TABLE
                AS
                RETURN
                    SELECT TOP (@tamanio_lote)
                        id,
                        codigo_tipo_evento,
                        agregado_id,
                        datos_evento,
                        creado_en,
                        procesado_en
                    FROM per_eventos_outbox
                    WHERE procesado_en IS NULL
                    ORDER BY creado_en;";

            await connection.ExecuteAsync(sql);
        }
    }
}
