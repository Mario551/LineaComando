using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola;
using PER.Comandos.LineaComandos.Cola.BaseDatos;
using PER.Comandos.LineaComandos.Cola.Resultados;
using System.Threading.Tasks;

namespace PER.Comandos.LineaComandos.Cola.Almacen
{
    public class AlmacenColaComandosPostgres : IAlmacenColaComandos
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public AlmacenColaComandosPostgres(string connectionString)
            : this(connectionString, "public")
        {
        }

        public AlmacenColaComandosPostgres(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.Postgres(esquema);
        }

        /// <summary>
        /// Encola un comando para procesamiento asíncrono.
        /// </summary>
        /// <param name="comando">Comando a encolar.</param>
        /// <param name="token">Token de cancelación.</param>
        /// <returns>ID del comando encolado.</returns>
        public async Task<long> EncolarAsync(ComandoEnCola comando, CancellationToken token = default)
        {
            string sql = $@"
                INSERT INTO {_nombres.ColaComandos} (
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
                FROM {_nombres.ComandosRegistrados} cr
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

            if (id == 0)
                throw new InvalidOperationException($"El comando '{comando.RutaComando}' no está registrado");

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
            string sql = $"SELECT * FROM {_nombres.ObtenerComandosPendientes}(@TamanioLote, @TimeoutMilisegundos);";

            using var connection = new NpgsqlConnection(_connectionString);
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

            string sql = $"SELECT * FROM {_nombres.MarcarComandosProcesando}(@Ids);";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var comandos = await connection.QueryAsync<DAO.ComandoEnCola>(sql,
                new { Ids = ids });

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
            await MarcarComoProcesadoAsync(comandoId, resultado, null, token);
        }

        public async Task MarcarComoProcesadoAsync(
            long comandoId,
            ResultadoComando resultado,
            PayloadResultadoComando? payloadResultado,
            CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.ColaComandos}
                SET fecha_ejecucion = @FechaEjecucion,
                    estado = @Estado,
                    mensaje_error = @MensajeError,
                    duracion_ms = @DuracionMs,
                    intentos = intentos + 1
                WHERE id = @ComandoId;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(
                sql,
                new
                {
                    ComandoId = comandoId,
                    FechaEjecucion = DateTime.Now,
                    Estado = resultado.Exitoso ? "completado" : "fallido",
                    resultado.MensajeError,
                    DuracionMs = (long)resultado.Duracion.TotalMilliseconds
                },
                transaction);

            await GuardarPayloadResultadoAsync(connection, transaction, comandoId, payloadResultado);

            transaction.Commit();
        }

        public async Task<ResultadoComandoPersistido?> ObtenerResultadoPersistidoAsync(
            long comandoId,
            CancellationToken token = default)
        {
            string sql = $@"
                SELECT
                    c.id AS ComandoId,
                    c.estado AS Estado,
                    c.mensaje_error AS MensajeError,
                    c.duracion_ms AS DuracionMs,
                    r.tipo AS ResultadoTipo,
                    r.version_resultado AS ResultadoVersion,
                    r.formato AS ResultadoFormato,
                    r.payload AS ResultadoPayload,
                    r.ruta_payload AS ResultadoRutaPayload
                FROM {_nombres.ColaComandos} c
                LEFT JOIN {_nombres.ColaComandosResultados} r ON r.comando_id = c.id
                WHERE c.id = @ComandoId;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            ResultadoComandoPersistidoFila? fila = await connection.QuerySingleOrDefaultAsync<ResultadoComandoPersistidoFila>(
                sql,
                new { ComandoId = comandoId });

            return fila is null ? null : MapToResultadoPersistido(fila);
        }

        public async Task ActualizarFechaLeidoAsync(long[] ids, CancellationToken token = default)
        {
            if (ids.Length == 0)
                return;

            string sql = $"CALL {_nombres.ActualizarFechaLeido}(@Ids);";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Ids = ids });
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

        private async Task GuardarPayloadResultadoAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long comandoId,
            PayloadResultadoComando? payloadResultado)
        {
            string eliminarSql = $"DELETE FROM {_nombres.ColaComandosResultados} WHERE comando_id = @ComandoId;";

            await connection.ExecuteAsync(eliminarSql, new { ComandoId = comandoId }, transaction);

            if (payloadResultado is null)
                return;

            string insertarSql = $@"
                INSERT INTO {_nombres.ColaComandosResultados} (
                    comando_id,
                    tipo,
                    version_resultado,
                    formato,
                    payload,
                    ruta_payload,
                    creado_en
                )
                VALUES (
                    @ComandoId,
                    @Tipo,
                    @Version,
                    @Formato,
                    @Payload,
                    @RutaPayload,
                    NOW()
                );";

            await connection.ExecuteAsync(
                insertarSql,
                new
                {
                    ComandoId = comandoId,
                    payloadResultado.Tipo,
                    payloadResultado.Version,
                    payloadResultado.Formato,
                    Payload = payloadResultado.Contenido,
                    payloadResultado.RutaPayload
                },
                transaction);
        }

        private static ResultadoComandoPersistido MapToResultadoPersistido(ResultadoComandoPersistidoFila fila)
        {
            PayloadResultadoComando? payloadResultado = string.IsNullOrWhiteSpace(fila.ResultadoTipo)
                ? null
                : new PayloadResultadoComando
                {
                    Tipo = fila.ResultadoTipo,
                    Version = fila.ResultadoVersion ?? 0,
                    Formato = fila.ResultadoFormato ?? "application/json",
                    Contenido = fila.ResultadoPayload,
                    RutaPayload = fila.ResultadoRutaPayload
                };

            return new ResultadoComandoPersistido
            {
                ComandoId = fila.ComandoId,
                Estado = fila.Estado,
                MensajeError = fila.MensajeError,
                Duracion = TimeSpan.FromMilliseconds(fila.DuracionMs ?? 0),
                PayloadResultado = payloadResultado
            };
        }

        private sealed class ResultadoComandoPersistidoFila
        {
            public long ComandoId { get; set; }

            public string Estado { get; set; } = string.Empty;

            public string? MensajeError { get; set; }

            public long? DuracionMs { get; set; }

            public string? ResultadoTipo { get; set; }

            public int? ResultadoVersion { get; set; }

            public string? ResultadoFormato { get; set; }

            public string? ResultadoPayload { get; set; }

            public string? ResultadoRutaPayload { get; set; }
        }
    }
}
