using Dapper;
using Npgsql;
using PER.Comandos.LineaComandos.Cola.BaseDatos;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class ColaEventosPostgres : IColaEventos
    {
        private readonly string _connectionString;
        private readonly NombresBaseDatos _nombres;

        public ColaEventosPostgres(string connectionString)
            : this(connectionString, "public")
        {
        }

        public ColaEventosPostgres(string connectionString, string esquema)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _nombres = NombresBaseDatos.Postgres(esquema);
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
                    @Datos::jsonb,
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
            string sql = $@"
                SELECT
                    id as Id,
                    codigo_tipo_evento as CodigoTipoEvento,
                    agregado_id as AgregadoId,
                    datos_evento::text as DatosEvento,
                    creado_en as CreadoEn,
                    procesado_en as ProcesadoEn
                FROM {_nombres.EventosOutbox}
                WHERE procesado_en IS NULL
                ORDER BY creado_en
                LIMIT @TamanioLote;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            return await connection.QueryAsync<EventoOutbox>(sql, new { TamanioLote = tamanioLote });
        }

        public async Task MarcarComoProcesadoAsync(long eventoId, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.EventosOutbox}
                SET procesado_en = NOW()
                WHERE id = @Id;";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Id = eventoId });
        }

        public async Task MarcarComoProcesadosAsync(IEnumerable<long> eventosIds, CancellationToken token = default)
        {
            string sql = $@"
                UPDATE {_nombres.EventosOutbox}
                SET procesado_en = NOW()
                WHERE id = ANY(@Ids);";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await connection.ExecuteAsync(sql, new { Ids = eventosIds.ToArray() });
        }
    }
}
