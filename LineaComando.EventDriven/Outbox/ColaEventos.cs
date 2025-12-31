using Dapper;
using Npgsql;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class ColaEventos : IColaEventos
    {
        private readonly string _connectionString;

        public ColaEventos(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<long> GuardarEventoAsync(DatosEvento datosEvento, CancellationToken token = default)
        {
            const string sql = @"
                INSERT INTO per_eventos_outbox (
                    codigo_tipo_evento,
                    agregado_id,
                    datos_evento,
                    metadatos,
                    creado_en
                )
                VALUES (
                    @TipoEvento,
                    @AgregadoId,
                    @Datos::jsonb,
                    @Metadatos::jsonb,
                    NOW()
                )
                RETURNING id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.ExecuteScalarAsync<long>(sql, datosEvento);
        }

        public async Task<IEnumerable<EventoOutbox>> ObtenerEventosPendientesAsync(
            int tamanioLote = 50,
            CancellationToken token = default)
        {
            const string sql = @"
                SELECT
                    id as Id,
                    codigo_tipo_evento as CodigoTipoEvento,
                    agregado_id as AgregadoId,
                    datos_evento::text as DatosEvento,
                    metadatos::text as Metadatos,
                    creado_en as CreadoEn,
                    procesado_en as ProcesadoEn
                FROM per_eventos_outbox
                WHERE procesado_en IS NULL
                ORDER BY creado_en
                LIMIT @TamanioLote;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<EventoOutbox>(sql, new { TamanioLote = tamanioLote });
        }

        public async Task MarcarComoProcesadoAsync(long eventoId, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_eventos_outbox
                SET procesado_en = NOW()
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = eventoId });
        }

        public async Task MarcarComoProcesadosAsync(IEnumerable<long> eventosIds, CancellationToken token = default)
        {
            const string sql = @"
                UPDATE per_eventos_outbox
                SET procesado_en = NOW()
                WHERE id = ANY(@Ids);";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Ids = eventosIds.ToArray() });
        }
    }
}
