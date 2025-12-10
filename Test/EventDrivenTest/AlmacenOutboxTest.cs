using Dapper;
using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace EventDrivenTest
{
    [Collection("DatabaseEventDriven")]
    public class AlmacenOutboxTest : BaseIntegracionTestEventDriven
    {
        private readonly AlmacenOutbox _almacen;

        protected override string PrefijoTest => "almacen_outbox_";

        public AlmacenOutboxTest(DatabaseFixtureEventDriven fixture) : base(fixture)
        {
            _almacen = new AlmacenOutbox(ConnectionString);
        }

        [Fact]
        public async Task GuardarEventoAsync_DebeInsertarEvento()
        {
            var datosEvento = new DatosEvento
            {
                TipoEvento = PrefijoTest + "pedido_creado",
                AgregadoId = 123,
                Datos = "{\"pedidoId\": 123, \"total\": 500}",
                Metadatos = "{\"correlationId\": \"abc-123\"}"
            };

            var id = await _almacen.GuardarEventoAsync(datosEvento);

            Assert.True(id > 0);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var evento = await connection.QuerySingleAsync<EventoOutbox>(
                @"SELECT id as Id, codigo_tipo_evento as CodigoTipoEvento, agregado_id as AgregadoId,
                  datos_evento::text as DatosEvento, metadatos::text as Metadatos,
                  creado_en as CreadoEn, procesado_en as ProcesadoEn
                  FROM eventos_outbox WHERE id = @Id",
                new { Id = id });

            Assert.Equal(datosEvento.TipoEvento, evento.CodigoTipoEvento);
            Assert.Equal(datosEvento.AgregadoId, evento.AgregadoId);
            Assert.Contains("pedidoId", evento.DatosEvento);
            Assert.Null(evento.ProcesadoEn);
        }

        [Fact]
        public async Task ObtenerEventosPendientesAsync_SinEventos_DebeRetornarVacio()
        {
            var eventos = await _almacen.ObtenerEventosPendientesAsync();

            var eventosFiltrados = eventos.Where(e => e.CodigoTipoEvento.StartsWith(PrefijoTest));
            Assert.Empty(eventosFiltrados);
        }

        [Fact]
        public async Task ObtenerEventosPendientesAsync_ConEventosPendientes_DebeRetornarEventos()
        {
            var datosEvento1 = new DatosEvento
            {
                TipoEvento = PrefijoTest + "evento_tipo_a",
                Datos = "{\"valor\": 1}"
            };
            var datosEvento2 = new DatosEvento
            {
                TipoEvento = PrefijoTest + "evento_tipo_b",
                Datos = "{\"valor\": 2}"
            };

            await _almacen.GuardarEventoAsync(datosEvento1);
            await _almacen.GuardarEventoAsync(datosEvento2);

            var eventos = await _almacen.ObtenerEventosPendientesAsync();

            var eventosFiltrados = eventos.Where(e => e.CodigoTipoEvento.StartsWith(PrefijoTest)).ToList();
            Assert.Equal(2, eventosFiltrados.Count);
        }

        [Fact]
        public async Task ObtenerEventosPendientesAsync_ConLimite_DebeRespetarLimite()
        {
            for (int i = 0; i < 5; i++)
            {
                var datosEvento = new DatosEvento
                {
                    TipoEvento = PrefijoTest + $"evento_lote_{i}",
                    Datos = $"{{\"index\": {i}}}"
                };
                await _almacen.GuardarEventoAsync(datosEvento);
            }

            var eventos = await _almacen.ObtenerEventosPendientesAsync(tamanioLote: 3);

            Assert.True(eventos.Count() <= 3);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_DebeActualizarFechaProcesado()
        {
            var datosEvento = new DatosEvento
            {
                TipoEvento = PrefijoTest + "evento_procesar",
                Datos = "{\"test\": true}"
            };
            var eventoId = await _almacen.GuardarEventoAsync(datosEvento);

            await _almacen.MarcarComoProcesadoAsync(eventoId);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var procesadoEn = await connection.QuerySingleAsync<DateTime?>(
                "SELECT procesado_en FROM eventos_outbox WHERE id = @Id",
                new { Id = eventoId });

            Assert.NotNull(procesadoEn);
        }

        [Fact]
        public async Task MarcarComoProcesadosAsync_VariosEventos_DebeActualizarTodos()
        {
            var ids = new List<long>();
            for (int i = 0; i < 3; i++)
            {
                var datosEvento = new DatosEvento
                {
                    TipoEvento = PrefijoTest + $"evento_batch_{i}",
                    Datos = $"{{\"index\": {i}}}"
                };
                ids.Add(await _almacen.GuardarEventoAsync(datosEvento));
            }

            await _almacen.MarcarComoProcesadosAsync(ids);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var procesados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM eventos_outbox WHERE id = ANY(@Ids) AND procesado_en IS NOT NULL",
                new { Ids = ids.ToArray() });

            Assert.Equal(3, procesados);
        }

        [Fact]
        public async Task ObtenerEventosPendientesAsync_EventoYaProcesado_NoDebeRetornarlo()
        {
            var datosEvento = new DatosEvento
            {
                TipoEvento = PrefijoTest + "evento_ya_procesado",
                Datos = "{\"procesado\": true}"
            };
            var eventoId = await _almacen.GuardarEventoAsync(datosEvento);
            await _almacen.MarcarComoProcesadoAsync(eventoId);

            var eventos = await _almacen.ObtenerEventosPendientesAsync();

            var eventosFiltrados = eventos.Where(e => e.CodigoTipoEvento.StartsWith(PrefijoTest));
            Assert.DoesNotContain(eventosFiltrados, e => e.Id == eventoId);
        }

        [Fact]
        public async Task ObtenerEventosPendientesAsync_DebeOrdenarPorFechaCreacion()
        {
            var ids = new List<long>();
            for (int i = 0; i < 3; i++)
            {
                var datosEvento = new DatosEvento
                {
                    TipoEvento = PrefijoTest + $"evento_orden_{i}",
                    Datos = $"{{\"orden\": {i}}}"
                };
                ids.Add(await _almacen.GuardarEventoAsync(datosEvento));
                await Task.Delay(10);
            }

            var eventos = await _almacen.ObtenerEventosPendientesAsync();

            var eventosFiltrados = eventos
                .Where(e => e.CodigoTipoEvento.StartsWith(PrefijoTest))
                .ToList();

            for (int i = 0; i < eventosFiltrados.Count - 1; i++)
            {
                Assert.True(eventosFiltrados[i].CreadoEn <= eventosFiltrados[i + 1].CreadoEn);
            }
        }

        [Fact]
        public void Constructor_ConnectionStringNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() => new AlmacenOutbox(null!));
        }
    }
}
