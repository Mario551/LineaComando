using Dapper;
using Microsoft.Data.SqlClient;

namespace PER.Comandos.LineaComandos.Cola.Esquema
{
    public class InicializadorEsquemaSqlServer
    {
        private readonly string _connectionString;

        public InicializadorEsquemaSqlServer(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public async Task InicializarAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await CrearTablaComandosRegistradosAsync(connection);
            await CrearTablaColaComandosAsync(connection);
            await CrearFuncionObtenerComandosPendientesAsync(connection);
            await CrearProcedimientoMarcarComandosProcesandoAsync(connection);
            await CrearProcedimientoActualizarFechaLeidoAsync(connection);
        }

        public async Task<bool> EsquemaExisteAsync(CancellationToken token = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var existeComandosRegistrados = await TablaExisteAsync(connection, "per_comandos_registrados");
            var existeColaComandos = await TablaExisteAsync(connection, "per_cola_comandos");

            return existeComandosRegistrados && existeColaComandos;
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

        private static async Task CrearTablaComandosRegistradosAsync(SqlConnection connection)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_comandos_registrados')
                BEGIN
                    CREATE TABLE per_comandos_registrados (
                        id INT IDENTITY(1,1) PRIMARY KEY,
                        ruta_comando NVARCHAR(2048) NOT NULL,
                        descripcion NVARCHAR(2048) NULL,
                        activo INT NOT NULL DEFAULT 1,
                        creado_en DATETIME2 NOT NULL DEFAULT GETDATE(),
                        actualizado_en DATETIME2 NULL,
                        CONSTRAINT uq_per_comandos_registrados_ruta UNIQUE (ruta_comando)
                    );

                    CREATE INDEX idx_per_comandos_registrados_ruta 
                        ON per_comandos_registrados(ruta_comando);

                    CREATE INDEX idx_per_comandos_registrados_activo 
                        ON per_comandos_registrados(activo);
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearTablaColaComandosAsync(SqlConnection connection)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'per_cola_comandos')
                BEGIN
                    CREATE TABLE per_cola_comandos (
                        id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        id_comando_registrado INT NOT NULL,
                        ruta_comando NVARCHAR(2048) NOT NULL,
                        argumentos NVARCHAR(2048) NULL,
                        datos_comando NVARCHAR(MAX) NULL,
                        fecha_creacion DATETIME2 NOT NULL DEFAULT GETDATE(),
                        fecha_leido DATETIME2 NULL,
                        fecha_ejecucion DATETIME2 NULL,
                        estado NVARCHAR(50) NOT NULL DEFAULT 'Pendiente',
                        mensaje_error NVARCHAR(MAX) NULL,
                        salida NVARCHAR(MAX) NULL,
                        duracion_ms BIGINT NULL,
                        intentos INT NOT NULL DEFAULT 0,

                        CONSTRAINT fk_per_cola_comandos_comando_registrado
                            FOREIGN KEY (id_comando_registrado)
                            REFERENCES per_comandos_registrados(id)
                            ON DELETE NO ACTION
                    );

                    CREATE INDEX idx_per_cola_comandos_estado 
                        ON per_cola_comandos(estado);

                    CREATE INDEX idx_per_cola_comandos_fecha_creacion 
                        ON per_cola_comandos(fecha_creacion);

                    CREATE INDEX idx_per_cola_comandos_fecha_leido 
                        ON per_cola_comandos(fecha_leido) 
                        WHERE fecha_leido IS NOT NULL;

                    CREATE INDEX idx_per_cola_comandos_pendientes 
                        ON per_cola_comandos(id, fecha_creacion) 
                        WHERE estado = 'Pendiente' AND fecha_leido IS NULL;
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearFuncionObtenerComandosPendientesAsync(SqlConnection connection)
        {
            const string sql = @"
                CREATE OR ALTER FUNCTION obtener_comandos_pendientes(
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
                           c.salida,
                           c.duracion_ms,
                           c.intentos
                    FROM per_cola_comandos c
                    WHERE (
                        (c.fecha_leido IS NULL AND c.estado = 'Pendiente')
                        OR
                        (c.estado = 'Procesando' AND c.fecha_leido < DATEADD(MILLISECOND, -@timeout_milisegundos, GETDATE()))
                    );";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearProcedimientoMarcarComandosProcesandoAsync(SqlConnection connection)
        {
            const string sql = @"
                CREATE OR ALTER PROCEDURE marcar_comandos_procesando
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
                        estado = 'Procesando'
                    FROM per_cola_comandos c
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
                           c.salida,
                           c.duracion_ms,
                           c.intentos
                    FROM per_cola_comandos c
                    INNER JOIN @IdTable t ON c.id = t.id;
                END";

            await connection.ExecuteAsync(sql);
        }

        private static async Task CrearProcedimientoActualizarFechaLeidoAsync(SqlConnection connection)
        {
            const string sql = @"
                CREATE OR ALTER PROCEDURE actualizar_fecha_leido
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
                    FROM per_cola_comandos c
                    INNER JOIN @IdTable t ON c.id = t.id
                    WHERE c.fecha_leido IS NULL;
                END";

            await connection.ExecuteAsync(sql);
        }
    }
}
