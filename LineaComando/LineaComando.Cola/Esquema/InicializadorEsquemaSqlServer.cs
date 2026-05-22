using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace PER.Comandos.LineaComandos.Cola.Esquema
{
    public class InicializadorEsquemaSqlServer
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public InicializadorEsquemaSqlServer(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        public InicializadorEsquemaSqlServer(string connectionString, string esquema)
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
            await CrearTablaComandosRegistradosAsync(connection);
            await CrearTablaEstadosColaComandosAsync(connection);
            await CrearTablaColaComandosAsync(connection);
            await CrearTablaResultadosColaComandosAsync(connection);
            await CrearFuncionObtenerComandosPendientesAsync(connection);
            await CrearProcedimientoMarcarComandosProcesandoAsync(connection);
            await CrearProcedimientoActualizarFechaLeidoAsync(connection);
        }

        public async Task<bool> EsquemaExisteAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var existeComandosRegistrados = await TablaExisteAsync(connection, "per_comandos_registrados");
            var existeEstadosColaComandos = await TablaExisteAsync(connection, "per_cola_comandos_estados");
            var existeColaComandos = await TablaExisteAsync(connection, "per_cola_comandos");
            var existeResultadosColaComandos = await TablaExisteAsync(connection, "per_cola_comandos_resultados");

            return existeComandosRegistrados && existeEstadosColaComandos && existeColaComandos && existeResultadosColaComandos;
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

        private async Task CrearTablaComandosRegistradosAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_comandos_registrados' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.ComandosRegistrados} (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        ruta_comando NVARCHAR(2048) NOT NULL,
                        descripcion NVARCHAR(2048) NULL,
                        activo INT NOT NULL DEFAULT 1,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
                        actualizado_en DATETIME2 NULL,
                        CONSTRAINT uq_per_comandos_registrados_ruta UNIQUE (ruta_comando)
                    );

                    CREATE INDEX idx_per_comandos_registrados_ruta 
                        ON {_nombres.ComandosRegistrados}(ruta_comando);

                    CREATE INDEX idx_per_comandos_registrados_activo 
                        ON {_nombres.ComandosRegistrados}(activo);
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaEstadosColaComandosAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_cola_comandos_estados' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.ColaComandosEstados} (
                        estado NVARCHAR(50) NOT NULL,
                        descripcion NVARCHAR(200) NOT NULL,
                        CONSTRAINT pk_per_cola_comandos_estados PRIMARY KEY (estado)
                    );
                END

                MERGE {_nombres.ColaComandosEstados} AS destino
                USING (VALUES
                    ('pendiente', 'Comando registrado y pendiente de tomar.'),
                    ('procesando', 'Comando tomado por un worker.'),
                    ('completado', 'Comando ejecutado correctamente.'),
                    ('fallido', 'Comando terminado con error.')
                ) AS origen (estado, descripcion)
                ON destino.estado = origen.estado
                WHEN MATCHED THEN
                    UPDATE SET descripcion = origen.descripcion
                WHEN NOT MATCHED THEN
                    INSERT (estado, descripcion)
                    VALUES (origen.estado, origen.descripcion);";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaColaComandosAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_cola_comandos' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.ColaComandos} (
                        id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        id_comando_registrado INT NOT NULL,
                        ruta_comando NVARCHAR(2048) NOT NULL,
                        argumentos NVARCHAR(2048) NULL,
                        datos_comando NVARCHAR(MAX) NULL,
                        fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
                        fecha_leido DATETIME2 NULL,
                        fecha_ejecucion DATETIME2 NULL,
                        estado NVARCHAR(50) NOT NULL CONSTRAINT df_per_cola_comandos_estado DEFAULT 'pendiente',
                        mensaje_error NVARCHAR(MAX) NULL,
                        duracion_ms BIGINT NULL,
                        intentos INT NOT NULL DEFAULT 0,

                        CONSTRAINT fk_per_cola_comandos_comando_registrado
                            FOREIGN KEY (id_comando_registrado)
                            REFERENCES {_nombres.ComandosRegistrados}(id)
                            ON DELETE NO ACTION,

                        CONSTRAINT fk_per_cola_comandos_estado
                            FOREIGN KEY (estado)
                            REFERENCES {_nombres.ColaComandosEstados}(estado)
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX idx_per_cola_comandos_estado 
                        ON {_nombres.ColaComandos}(estado);

                    CREATE INDEX idx_per_cola_comandos_fecha_creacion 
                        ON {_nombres.ColaComandos}(fecha_creacion);

                    CREATE INDEX idx_per_cola_comandos_fecha_leido
                        ON {_nombres.ColaComandos}(fecha_leido)
                        WHERE fecha_leido IS NOT NULL;

                    CREATE INDEX idx_per_cola_comandos_pendientes
                        ON {_nombres.ColaComandos}(id, fecha_creacion)
                        WHERE estado = 'pendiente' AND fecha_leido IS NULL;
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearTablaResultadosColaComandosAsync(SqlConnection connection)
        {
            string sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_cola_comandos_resultados' AND schema_id = SCHEMA_ID(N'{_nombres.Esquema}'))
                BEGIN
                    CREATE TABLE {_nombres.ColaComandosResultados} (
                        comando_id BIGINT NOT NULL,
                        tipo NVARCHAR(200) NOT NULL,
                        version_resultado INT NOT NULL,
                        formato NVARCHAR(100) NOT NULL,
                        payload NVARCHAR(MAX) NULL,
                        ruta_payload NVARCHAR(2048) NULL,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),

                        CONSTRAINT pk_per_cola_comandos_resultados PRIMARY KEY (comando_id),

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

                    CREATE INDEX idx_per_cola_comandos_resultados_tipo_version
                        ON {_nombres.ColaComandosResultados}(tipo, version_resultado);
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearFuncionObtenerComandosPendientesAsync(SqlConnection connection)
        {
            string sql = $@"
                CREATE OR ALTER FUNCTION {_nombres.ObtenerComandosPendientes}(
                    @tamanio_lote INT = 50,
                    @timeout_milisegundos INT = 300000
                )
                RETURNS TABLE
                AS
                RETURN
                    SELECT c.id,
                           c.id_comando_registrado,
                           c.ruta_comando,
                           c.argumentos,
                           c.datos_comando,
                           c.fecha_creacion,
                           c.fecha_leido,
                           c.fecha_ejecucion,
                           c.estado,
                           c.mensaje_error,
                           c.duracion_ms,
                           c.intentos
                    FROM {_nombres.ColaComandos} c
                    WHERE (
                        (c.fecha_leido IS NULL AND c.estado = 'pendiente')
                        OR
                        (c.estado = 'procesando' AND c.fecha_leido < DATEADD(MILLISECOND, -@timeout_milisegundos, GETDATE()))
                    );";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearProcedimientoMarcarComandosProcesandoAsync(SqlConnection connection)
        {
            string sql = $@"
                CREATE OR ALTER PROCEDURE {_nombres.MarcarComandosProcesando}
                    @ids NVARCHAR(MAX)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @IdTable TABLE (id BIGINT);
                    
                    INSERT INTO @IdTable (id)
                    SELECT CAST(value AS BIGINT)
                    FROM STRING_SPLIT(@ids, ',');
                    
                    UPDATE c
                    SET fecha_leido = GETDATE(),
                        estado = 'procesando'
                    FROM {_nombres.ColaComandos} c
                    INNER JOIN @IdTable t ON c.id = t.id;
                    
                    SELECT c.id,
                           c.id_comando_registrado,
                           c.ruta_comando,
                           c.argumentos,
                           c.datos_comando,
                           c.fecha_creacion,
                           c.fecha_leido,
                           c.fecha_ejecucion,
                           c.estado,
                           c.mensaje_error,
                           c.duracion_ms,
                           c.intentos
                    FROM {_nombres.ColaComandos} c
                    INNER JOIN @IdTable t ON c.id = t.id;
                END";

            await connection.ExecuteAsync(sql);
        }

        private async Task CrearProcedimientoActualizarFechaLeidoAsync(SqlConnection connection)
        {
            string sql = $@"
                CREATE OR ALTER PROCEDURE {_nombres.ActualizarFechaLeido}
                    @ids NVARCHAR(MAX)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @IdTable TABLE (id BIGINT);
                    
                    INSERT INTO @IdTable (id)
                    SELECT CAST(value AS BIGINT)
                    FROM STRING_SPLIT(@ids, ',');
                    
                    UPDATE c
                    SET fecha_leido = GETDATE()
                    FROM {_nombres.ColaComandos} c
                    INNER JOIN @IdTable t ON c.id = t.id
                    WHERE c.fecha_leido IS NULL;
                END";

            await connection.ExecuteAsync(sql);
        }
    }
}
