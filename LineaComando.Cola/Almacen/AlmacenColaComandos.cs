using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola;
using System.Threading.Tasks;

namespace PER.Comandos.LineaComandos.Cola.Almacen
{
    public class AlmacenColaComandos : IAlmacenColaComandos
    {
        private readonly string _connectionString;

        public AlmacenColaComandos(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Encola un comando para procesamiento asíncrono.
        /// </summary>
        /// <param name="comando">Comando a encolar.</param>
        /// <param name="token">Token de cancelación.</param>
        /// <returns>ID del comando encolado.</returns>
        public async Task<long> EncolarAsync(ComandoEnCola comando, CancellationToken token = default)
        {
            const string sql = @"
                INSERT INTO cola_comandos (
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
                    @DatosDeComando::jsonb,
                    @FechaCreacion,
                    @Estado,
                    @Intentos
                FROM comandos_registrados cr
                WHERE cr.ruta_comando = @RutaComando
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var id = await connection.ExecuteScalarAsync<long>(
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

            return id;
        }

        /// <summary>
        /// Obtiene comandos pendientes de procesamiento.
        /// </summary>
        /// <param name="tamanioLote">Número máximo de comandos a obtener.</param>
        /// <param name="token">Token de cancelación.</param>
        /// <returns>Colección de comandos pendientes.</returns>
        public async Task<IEnumerable<ComandoEnCola>> ObtenerComandosPendientesAsync(
            int tamanioLote = 50,
            CancellationToken token = default)
        {
            const string sql = "SELECT * FROM obtener_comandos_pendientes(@TamanioLote, @TimeoutMilisegundos);";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var comandos = await connection.QueryAsync<DAO.ComandoEnCola>(sql,
            new
            {
                TamanioLote = tamanioLote,
                TimeoutMilisegundos = 300000 // 5 minutos de timeout por defecto
            });

            return comandos.Select(MapToComandoEnCola);
        }

        /// <summary>
        /// Marca un comando como procesado con su resultado.
        /// </summary>
        /// <param name="comandoId">ID del comando procesado.</param>
        /// <param name="resultado">Resultado de la ejecución.</param>
        /// <param name="token">Token de cancelación.</param>
        public async Task MarcarComoProcesadoAsync(
            long comandoId,
            ResultadoComando resultado,
            CancellationToken token = default)
        {
            const string sql = @"
                UPDATE cola_comandos
                SET fecha_ejecucion = @FechaEjecucion,
                    estado = @Estado,
                    mensaje_error = @MensajeError,
                    salida = @Salida,
                    duracion_ms = @DuracionMs,
                    intentos = intentos + 1
                WHERE id = @ComandoId;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(
                sql,
                new
                {
                    ComandoId = comandoId,
                    FechaEjecucion = DateTime.UtcNow,
                    Estado = resultado.Exitoso ? "Completado" : "Fallido",
                    resultado.MensajeError,
                    resultado.Salida,
                    DuracionMs = (long)resultado.Duracion.TotalMilliseconds
                });
        }

        /// <summary>
        /// Mapea un DAO de base de datos a un objeto de dominio ComandoEnCola.
        /// </summary>
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