using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace PER.Comandos.LineaComandos.EventDriven.Esquema
{
    public class InicializadorEsquemaEventDrivenSqlServer
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public InicializadorEsquemaEventDrivenSqlServer(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        public InicializadorEsquemaEventDrivenSqlServer(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.SqlServer(esquema);
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public async Task InicializarAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await CrearEsquemaAsync(connection);

            if (!await TablaExisteAsync(connection, "per_comandos_registrados"))
            {
                throw new InvalidOperationException(
                    "La tabla 'per_comandos_registrados' no existe. " +
                    "Debe inicializar el esquema de LineaComando.Cola primero.");
            }

            await CrearTablaTiposEventoAsync(connection);
            await CrearTablaManejadoresEventoAsync(connection);
            await CrearTablaDisparadoresManejadorAsync(connection);
            await MigrarTablaDisparadoresCodigoAsync(connection);
            await MigrarTablaDisparadoresAsync(connection);
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

        private async Task<bool> TablaExisteAsync(SqlConnection connection, string nombreTabla)
        {
            const string sql = @"
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM sys.tables 
                    WHERE name = @NombreTabla
                    AND schema_id = SCHEMA_ID(@Esquema)
                ) THEN 1 ELSE 0 END AS BIT);";

            return await connection.ExecuteScalarAsync<bool>(sql, new { _nombres.Esquema, NombreTabla = nombreTabla });
        }

        private async Task CrearEsquemaAsync(SqlConnection connection)
        {
            string sql = $@"
                IF SCHEMA_ID(N'{_nombres.Esquema}') IS NULL
                    EXEC(N'CREATE SCHEMA {_nombres.EsquemaSql}');";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaTiposEventoAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_tipos_evento' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.TiposEvento} (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        codigo NVARCHAR(255) NOT NULL UNIQUE,
                        nombre NVARCHAR(255) NOT NULL,
                        descripcion NVARCHAR(2048) NULL,
                        activo INT NOT NULL DEFAULT 1,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE()
                    );

                    CREATE INDEX idx_per_tipos_evento_codigo 
                        ON {_nombres.TiposEvento}(codigo);

                    CREATE INDEX idx_per_tipos_evento_activo 
                        ON {_nombres.TiposEvento}(activo);
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaManejadoresEventoAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_manejadores_evento' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.ManejadoresEvento} (
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
                            REFERENCES {_nombres.ComandosRegistrados}(id)
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX idx_per_manejadores_evento_codigo 
                        ON {_nombres.ManejadoresEvento}(codigo);

                    CREATE INDEX idx_per_manejadores_evento_activo 
                        ON {_nombres.ManejadoresEvento}(activo);

                    CREATE INDEX idx_per_manejadores_evento_comando 
                        ON {_nombres.ManejadoresEvento}(id_comando_registrado);
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaDisparadoresManejadorAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_disparadores_manejador' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.DisparadoresManejador} (
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
                            REFERENCES {_nombres.ManejadoresEvento}(id)
                            ON DELETE CASCADE,

                        CONSTRAINT fk_disparador_tipo_evento
                            FOREIGN KEY (tipo_evento_id)
                            REFERENCES {_nombres.TiposEvento}(id)
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
                        ON {_nombres.DisparadoresManejador}(manejador_evento_id);

                    CREATE INDEX idx_disparadores_tipo_evento
                        ON {_nombres.DisparadoresManejador}(tipo_evento_id)
                        WHERE tipo_evento_id IS NOT NULL;

                    CREATE INDEX idx_disparadores_modo 
                        ON {_nombres.DisparadoresManejador}(modo_disparo, activo);

                    CREATE INDEX idx_disparadores_programados
                        ON {_nombres.DisparadoresManejador}(modo_disparo, activo, expresion)
                        WHERE modo_disparo = 'Programado';
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task MigrarTablaDisparadoresCodigoAsync(SqlConnection connection)
        {
            string sqlVerificar = $@"
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'{_nombres.Esquema}.per_disparadores_manejador')
                    AND name = 'codigo'
                ) THEN 1 ELSE 0 END AS BIT);";

            bool columnaExiste = await connection.ExecuteScalarAsync<bool>(sqlVerificar);

            if (!columnaExiste)
            {
                string sqlAgregar = $@"
                    ALTER TABLE {_nombres.DisparadoresManejador}
                    ADD codigo NVARCHAR(255) NOT NULL UNIQUE;";

                await connection.ExecuteAsync(sqlAgregar);
            }
        }

        private async Task MigrarTablaDisparadoresAsync(SqlConnection connection)
        {
            string sqlVerificar = $@"
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'{_nombres.Esquema}.per_disparadores_manejador')
                    AND name = 'ultima_ejecucion'
                ) THEN 1 ELSE 0 END AS BIT);";

            bool columnaExiste = await connection.ExecuteScalarAsync<bool>(sqlVerificar);

            if (!columnaExiste)
            {
                string sqlAgregar = $@"
                    ALTER TABLE {_nombres.DisparadoresManejador}
                    ADD ultima_ejecucion DATETIME2 NULL;";

                await connection.ExecuteAsync(sqlAgregar);
            }
        }

        private async Task CrearTablaEventosOutboxAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_eventos_outbox' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.EventosOutbox} (
                        id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        codigo_tipo_evento NVARCHAR(255) NOT NULL,
                        agregado_id BIGINT NULL,
                        datos_evento NVARCHAR(MAX) NOT NULL,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
                        procesado_en DATETIME2 NULL
                    );

                    CREATE INDEX idx_per_eventos_outbox_tipo 
                        ON {_nombres.EventosOutbox}(codigo_tipo_evento);

                    CREATE INDEX idx_per_eventos_outbox_procesado
                        ON {_nombres.EventosOutbox}(procesado_en)
                        WHERE procesado_en IS NULL;

                    CREATE INDEX idx_per_eventos_outbox_creado 
                        ON {_nombres.EventosOutbox}(creado_en);

                    CREATE INDEX idx_per_eventos_outbox_pendientes
                        ON {_nombres.EventosOutbox}(codigo_tipo_evento, creado_en)
                        WHERE procesado_en IS NULL;
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearFuncionObtenerEventosPendientesAsync(SqlConnection connection)
        {
            string sql = $@"
                CREATE OR ALTER FUNCTION {_nombres.ObtenerEventosPendientes}(
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
                    FROM {_nombres.EventosOutbox}
                    WHERE procesado_en IS NULL
                    ORDER BY creado_en;";

            await connection.ExecuteAsync(sql);
        }
    }
}
