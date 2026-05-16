using Dapper;
using Microsoft.Data.SqlClient;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class ColaEventosSqlServer : IColaEventos
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public ColaEventosSqlServer(string connectionString)
            : this(connectionString, "dbo")
        {
        }

        public ColaEventosSqlServer(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.SqlServer(esquema);
        }

        public async Task<long> GuardarEventoAsync(DatosEvento datosEvento, CancellationToken token = default)
        {
            string sql = $@"
                INSERT INTO {_nombres.EventosOutbox} (
                    codigo_tipo_evento,
                    agregado_id,
                    datos_evento,
                    creado_en
                )
                VALUES (
                    @TipoEvento,
                    @AgregadoId,
                    @Datos,
                    GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.ExecuteScalarAsync<long>(sql, datosEvento);
        }

        public async Task<IEnumerable<EventoOutbox>> ObtenerEventosPendientesAsync(
            int tamanioLote = 50,
            CancellationToken token = default)
        {
            string sql = $@"
                SELECT TOP(@TamanioLote)
                    id,
                    codigo_tipo_evento,
                    agregado_id,
                    datos_evento,
                    creado_en,
                    procesado_en
                FROM {_nombres.EventosOutbox}
                WHERE procesado_en IS NULL
                ORDER BY creado_en;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<EventoOutbox>(sql, new { TamanioLote = tamanioLote });
        }

        public async Task MarcarComoProcesadoAsync(long eventoId, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.EventosOutbox}
                SET procesado_en = GETDATE()
                WHERE id = @Id;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = eventoId });
        }

        public async Task MarcarComoProcesadosAsync(IEnumerable<long> eventosIds, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.EventosOutbox}
                SET procesado_en = GETDATE()
                WHERE id IN @Ids;";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Ids = eventosIds });
        }
    }
}
