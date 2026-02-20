using Dapper;
using Microsoft.Data.SqlClient;

namespace PER.Comandos.LineaComandos.Cola.Almacen
{
    public class AlmacenColaComandosSqlServer : IAlmacenColaComandos
    {
        private readonly string _connectionString;

        public AlmacenColaComandosSqlServer(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<long> EncolarAsync(ComandoEnCola comando, CancellationToken token = default)
        {
            const string sql = @"
                INSERT INTO per_cola_comandos (
                    id_comando_registrado,
                    ruta_comando,
                    argumentos,
                    datos_comando,
                    fecha_creacion,
                    estado,
                    intentos
                )
                SELECT
                    cr.id,
                    @RutaComando,
                    @Argumentos,
                    @DatosDeComando,
                    @FechaCreacion,
                    @Estado,
                    @Intentos
                FROM per_comandos_registrados cr
                WHERE cr.ruta_comando = @RutaComando;

                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            long id = await connection.ExecuteScalarAsync<long>(
                sql,
                new
                {
                    comando.RutaComando,
                    comando.Argumentos,
                    comando.DatosDeComando,
                    comando.FechaCreacion,
                    comando.Estado,
                    comando.Intentos
                });

            if (id == 0)
                throw new InvalidOperationException($"El comando '{comando.RutaComando}' no está registrado");

            return id;
        }

        public async Task<IEnumerable<ComandoEnCola>> ObtenerComandosPendientesAsync(
            int tamanioLote = 50,
            CancellationToken token = default)
        {
            const string sql = @"
                SELECT TOP (@TamanioLote) 
                    c.id,
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
                FROM dbo.obtener_comandos_pendientes(@TamanioLote, @TimeoutMilisegundos) c
                ORDER BY c.id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var comandos = await connection.QueryAsync<DAO.ComandoEnCola>(sql,
                new
                {
                    TamanioLote = tamanioLote,
                    TimeoutMilisegundos = 300000
                });

            return comandos.Select(MapToComandoEnCola);
        }

        public async Task<IEnumerable<ComandoEnCola>> MarcarComandosProcesandoAsync(
            long[] ids,
            CancellationToken token = default)
        {
            if (ids.Length == 0)
                return Enumerable.Empty<ComandoEnCola>();

            const string sql = "EXEC marcar_comandos_procesando @Ids;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var comandos = await connection.QueryAsync<DAO.ComandoEnCola>(sql,
                new { Ids = string.Join(",", ids) });

            return comandos.Select(MapToComandoEnCola);
        }

        public async Task MarcarComoProcesadoAsync(
            long comandoId,
            ResultadoComando resultado,
            CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_cola_comandos
                SET fecha_ejecucion = @FechaEjecucion,
                    estado = @Estado,
                    mensaje_error = @MensajeError,
                    salida = @Salida,
                    duracion_ms = @DuracionMs,
                    intentos = intentos + 1
                WHERE id = @ComandoId;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(
                sql,
                new
                {
                    ComandoId = comandoId,
                    FechaEjecucion = DateTime.Now,
                    Estado = resultado.Exitoso ? "Completado" : "Fallido",
                    resultado.MensajeError,
                    resultado.Salida,
                    DuracionMs = (long)resultado.Duracion.TotalMilliseconds
                });
        }

        public async Task ActualizarFechaLeidoAsync(long[] ids, CancellationToken token = default)
        {
            if (ids.Length == 0)
                return;

            const string sql = "EXEC actualizar_fecha_leido @Ids;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Ids = string.Join(",", ids) });
        }

        private static ComandoEnCola MapToComandoEnCola(DAO.ComandoEnCola dao)
        {
            return new ComandoEnCola
            {
                Id = dao.Id,
                RutaComando = dao.RutaComando,
                Argumentos = dao.Argumentos ?? string.Empty,
                DatosDeComando = dao.DatosComando,
                FechaCreacion = dao.FechaCreacion,
                FechaEjecucion = dao.FechaEjecucion,
                Estado = dao.Estado,
                MensajeError = dao.MensajeError,
                Intentos = dao.Intentos
            };
        }
    }
}
