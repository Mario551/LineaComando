using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PER.Comandos.LineaComandos.EventDriven.Outbox;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Servicio;
using PER.Comandos.LineaComandos.Cola.Almacen;

namespace EventDrivenTest
{
    public class ProcesadorEventosTest
    {
        private readonly Mock<IAlmacenOutbox> _mockAlmacenOutbox;
        private readonly Mock<IRegistroManejadores> _mockRegistroManejadores;
        private readonly Mock<IAlmacenColaComandos> _mockAlmacenCola;
        private readonly ILogger _logger;

        public ProcesadorEventosTest()
        {
            _mockAlmacenOutbox = new Mock<IAlmacenOutbox>();
            _mockRegistroManejadores = new Mock<IRegistroManejadores>();
            _mockAlmacenCola = new Mock<IAlmacenColaComandos>();
            _logger = NullLogger.Instance;
        }

        [Fact]
        public void Constructor_AlmacenOutboxNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorEventos(
                    null!,
                    _mockRegistroManejadores.Object,
                    _mockAlmacenCola.Object,
                    _logger));
        }

        [Fact]
        public void Constructor_RegistroManejadoresNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorEventos(
                    _mockAlmacenOutbox.Object,
                    null!,
                    _mockAlmacenCola.Object,
                    _logger));
        }

        [Fact]
        public void Constructor_AlmacenColaNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorEventos(
                    _mockAlmacenOutbox.Object,
                    _mockRegistroManejadores.Object,
                    null!,
                    _logger));
        }

        [Fact]
        public void Constructor_LoggerNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorEventos(
                    _mockAlmacenOutbox.Object,
                    _mockRegistroManejadores.Object,
                    _mockAlmacenCola.Object,
                    null!));
        }

        [Fact]
        public async Task ProcesarLoteAsync_SinEventos_NoDebeHacerNada()
        {
            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<EventoOutbox>());

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            _mockAlmacenOutbox.Verify(
                a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _mockRegistroManejadores.Verify(
                r => r.ObtenerManejadoresParaEventoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _mockAlmacenCola.Verify(
                c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcesarLoteAsync_ConEvento_DebeEncolarComandos()
        {
            var evento = new EventoOutbox
            {
                Id = 1,
                CodigoTipoEvento = "pedido_creado",
                DatosEvento = "{\"pedidoId\": 123}",
                CreadoEn = DateTime.UtcNow
            };

            var configuracion = new ConfiguracionManejador
            {
                Id = 1,
                IDManejador = 1,
                IdComandoRegistrado = 1,
                RutaComando = "notificacion email",
                ArgumentosComando = "--tipo=pedido",
                ModoDisparo = "Evento",
                CodigoTipoEvento = "pedido_creado",
                Activo = true,
                Prioridad = 0
            };

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync("pedido_creado", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { configuracion });

            _mockAlmacenCola
                .Setup(c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1L);

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            _mockAlmacenCola.Verify(
                c => c.EncolarAsync(
                    It.Is<ComandoEnCola>(cmd =>
                        cmd.RutaComando == "notificacion email" &&
                        cmd.Argumentos == "--tipo=pedido" &&
                        cmd.DatosDeComando == evento.DatosEvento),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockAlmacenOutbox.Verify(
                a => a.MarcarComoProcesadoAsync(evento.Id, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcesarLoteAsync_EventoSinHandlers_DebeMarcarProcesado()
        {
            var evento = new EventoOutbox
            {
                Id = 1,
                CodigoTipoEvento = "evento_sin_handlers",
                DatosEvento = "{}",
                CreadoEn = DateTime.UtcNow
            };

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync("evento_sin_handlers", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<ConfiguracionManejador>());

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            _mockAlmacenCola.Verify(
                c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _mockAlmacenOutbox.Verify(
                a => a.MarcarComoProcesadoAsync(evento.Id, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcesarLoteAsync_ConMultiplesHandlers_DebeEncolarTodos()
        {
            var evento = new EventoOutbox
            {
                Id = 1,
                CodigoTipoEvento = "pedido_creado",
                DatosEvento = "{\"pedidoId\": 123}",
                CreadoEn = DateTime.UtcNow
            };

            var configuraciones = new[]
            {
                new ConfiguracionManejador
                {
                    Id = 1,
                    RutaComando = "handler1",
                    Activo = true,
                    Prioridad = 0
                },
                new ConfiguracionManejador
                {
                    Id = 2,
                    RutaComando = "handler2",
                    Activo = true,
                    Prioridad = 1
                }
            };

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync("pedido_creado", It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuraciones);

            _mockAlmacenCola
                .Setup(c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1L);

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            _mockAlmacenCola.Verify(
                c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ProcesarLoteAsync_ConCancelacion_DebeDetenerse()
        {
            var eventos = new[]
            {
                new EventoOutbox { Id = 1, CodigoTipoEvento = "evento1", DatosEvento = "{}", CreadoEn = DateTime.UtcNow },
                new EventoOutbox { Id = 2, CodigoTipoEvento = "evento2", DatosEvento = "{}", CreadoEn = DateTime.UtcNow }
            };

            using var cts = new CancellationTokenSource();

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(eventos);

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => cts.Cancel())
                .ReturnsAsync(Enumerable.Empty<ConfiguracionManejador>());

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync(token: cts.Token);

            _mockRegistroManejadores.Verify(
                r => r.ObtenerManejadoresParaEventoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcesarLoteAsync_ConTamanioLote_DebeUsarTamanioEspecificado()
        {
            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<EventoOutbox>());

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync(tamanioLote: 100);

            _mockAlmacenOutbox.Verify(
                a => a.ObtenerEventosPendientesAsync(100, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcesarLoteAsync_ConMultiplesEventos_DebeProcesarTodos()
        {
            var eventos = new[]
            {
                new EventoOutbox { Id = 1, CodigoTipoEvento = "tipo1", DatosEvento = "{}", CreadoEn = DateTime.UtcNow },
                new EventoOutbox { Id = 2, CodigoTipoEvento = "tipo2", DatosEvento = "{}", CreadoEn = DateTime.UtcNow },
                new EventoOutbox { Id = 3, CodigoTipoEvento = "tipo3", DatosEvento = "{}", CreadoEn = DateTime.UtcNow }
            };

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(eventos);

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<ConfiguracionManejador>());

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            _mockAlmacenOutbox.Verify(
                a => a.MarcarComoProcesadoAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task ProcesarLoteAsync_HandlerInactivo_NoDebeEncolar()
        {
            var evento = new EventoOutbox
            {
                Id = 1,
                CodigoTipoEvento = "evento_handler_inactivo",
                DatosEvento = "{}",
                CreadoEn = DateTime.UtcNow
            };

            var configuracion = new ConfiguracionManejador
            {
                Id = 1,
                RutaComando = "handler_inactivo",
                Activo = false,
                Prioridad = 0
            };

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync("evento_handler_inactivo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { configuracion });

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            _mockAlmacenCola.Verify(
                c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcesarLoteAsync_ErrorEnEncolar_DebePropagarExcepcion()
        {
            var evento = new EventoOutbox
            {
                Id = 1,
                CodigoTipoEvento = "evento_error",
                DatosEvento = "{}",
                CreadoEn = DateTime.UtcNow
            };

            var configuracion = new ConfiguracionManejador
            {
                Id = 1,
                RutaComando = "handler",
                Activo = true,
                Prioridad = 0
            };

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync("evento_error", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { configuracion });

            _mockAlmacenCola
                .Setup(c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Error al encolar"));

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await Assert.ThrowsAsync<InvalidOperationException>(() => procesador.ProcesarLoteAsync());
        }

        [Fact]
        public async Task ProcesarLoteAsync_DebeRespetarPrioridadDeHandlers()
        {
            var evento = new EventoOutbox
            {
                Id = 1,
                CodigoTipoEvento = "evento_prioridad",
                DatosEvento = "{}",
                CreadoEn = DateTime.UtcNow
            };

            var configuraciones = new[]
            {
                new ConfiguracionManejador { Id = 1, RutaComando = "handler_bajo", Activo = true, Prioridad = 10 },
                new ConfiguracionManejador { Id = 2, RutaComando = "handler_alto", Activo = true, Prioridad = 1 }
            };

            var ordenEjecucion = new List<string>();

            _mockAlmacenOutbox
                .Setup(a => a.ObtenerEventosPendientesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });

            _mockRegistroManejadores
                .Setup(r => r.ObtenerManejadoresParaEventoAsync("evento_prioridad", It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuraciones);

            _mockAlmacenCola
                .Setup(c => c.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()))
                .Callback<ComandoEnCola, CancellationToken>((cmd, _) => ordenEjecucion.Add(cmd.RutaComando))
                .ReturnsAsync(1L);

            var procesador = new ProcesadorEventos(
                _mockAlmacenOutbox.Object,
                _mockRegistroManejadores.Object,
                _mockAlmacenCola.Object,
                _logger);

            await procesador.ProcesarLoteAsync();

            Assert.Equal(2, ordenEjecucion.Count);
            Assert.Equal("handler_alto", ordenEjecucion[0]);
            Assert.Equal("handler_bajo", ordenEjecucion[1]);
        }
    }
}
