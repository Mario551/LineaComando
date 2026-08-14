using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PER.Comandos.LineaComandos.EventDriven.Bus;
using PER.Comandos.LineaComandos.EventDriven.Colas;
using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace EventDrivenTest
{
    public class RegistrarEventoTest
    {
        [Fact]
        public async Task RegistrarEnColaAsync_DebePersistirEncolarYNotificarEnOrden()
        {
            List<string> orden = new List<string>();

            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.GuardarEventoAsync(
                    It.Is<DatosEvento>(d =>
                        d.TipoEvento == "pedido_creado" &&
                        d.AgregadoId == 77 &&
                        d.Datos.Contains("123")),
                    It.IsAny<CancellationToken>()))
                .Callback<DatosEvento, CancellationToken>((_, _) => orden.Add("persistido"))
                .ReturnsAsync(15);

            Mock<IColaEventosMemoria> colaEventosMemoria = new Mock<IColaEventosMemoria>();
            colaEventosMemoria
                .Setup(c => c.EncolarAsync(
                    It.Is<EventoOutbox>(e =>
                        e.Id == 15 &&
                        e.CodigoTipoEvento == "pedido_creado" &&
                        e.AgregadoId == 77 &&
                        e.DatosEvento.Contains("123")),
                    It.IsAny<CancellationToken>()))
                .Callback<EventoOutbox, CancellationToken>((_, _) => orden.Add("encolado"))
                .Returns(Task.CompletedTask);

            Mock<IPublicadorNotificacionEventos> publicador =
                new Mock<IPublicadorNotificacionEventos>();
            publicador
                .Setup(p => p.Notificar(It.Is<NotificacionEventoLanzado>(n =>
                    n.Id == 15 &&
                    n.NombreEvento == "pedido_creado" &&
                    n.AgregadoId == 77 &&
                    n.DatosEvento.Contains("123"))))
                .Callback<NotificacionEventoLanzado>(_ => orden.Add("notificado"));

            RegistrarEvento registrarEvento = CrearRegistrar(
                colaEventos.Object,
                colaEventosMemoria.Object,
                publicador.Object);
            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            await registrarEvento.RegistrarEnColaAsync();

            Assert.Equal(new[] { "persistido", "encolado", "notificado" }, orden);
            publicador.Verify(
                p => p.Notificar(It.IsAny<NotificacionEventoLanzado>()),
                Times.Once);
        }

        [Fact]
        public async Task RegistrarEnColaAsync_SiFallaPersistencia_NoDebeEncolarNiNotificar()
        {
            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.GuardarEventoAsync(
                    It.IsAny<DatosEvento>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("fallo persistiendo"));
            Mock<IColaEventosMemoria> colaEventosMemoria = new Mock<IColaEventosMemoria>();
            Mock<IPublicadorNotificacionEventos> publicador =
                new Mock<IPublicadorNotificacionEventos>();
            RegistrarEvento registrarEvento = CrearRegistrar(
                colaEventos.Object,
                colaEventosMemoria.Object,
                publicador.Object);
            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => registrarEvento.RegistrarEnColaAsync());

            colaEventosMemoria.Verify(
                c => c.EncolarAsync(It.IsAny<EventoOutbox>(), It.IsAny<CancellationToken>()),
                Times.Never);
            publicador.Verify(
                p => p.Notificar(It.IsAny<NotificacionEventoLanzado>()),
                Times.Never);
        }

        [Fact]
        public async Task RegistrarEnColaAsync_SiFallaEncolado_NoDebeNotificarNiFallar()
        {
            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.GuardarEventoAsync(
                    It.IsAny<DatosEvento>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(15);
            Mock<IColaEventosMemoria> colaEventosMemoria = new Mock<IColaEventosMemoria>();
            colaEventosMemoria
                .Setup(c => c.EncolarAsync(
                    It.IsAny<EventoOutbox>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("fallo en memoria"));
            Mock<IPublicadorNotificacionEventos> publicador =
                new Mock<IPublicadorNotificacionEventos>();
            RegistrarEvento registrarEvento = CrearRegistrar(
                colaEventos.Object,
                colaEventosMemoria.Object,
                publicador.Object);
            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            await registrarEvento.RegistrarEnColaAsync();

            colaEventos.Verify(
                c => c.GuardarEventoAsync(It.IsAny<DatosEvento>(), It.IsAny<CancellationToken>()),
                Times.Once);
            publicador.Verify(
                p => p.Notificar(It.IsAny<NotificacionEventoLanzado>()),
                Times.Never);
        }

        [Fact]
        public async Task RegistrarEnColaAsync_SiFallaNotificacion_NoDebeFallar()
        {
            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.GuardarEventoAsync(
                    It.IsAny<DatosEvento>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(15);
            Mock<IColaEventosMemoria> colaEventosMemoria = new Mock<IColaEventosMemoria>();
            colaEventosMemoria
                .Setup(c => c.EncolarAsync(
                    It.IsAny<EventoOutbox>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Mock<IPublicadorNotificacionEventos> publicador =
                new Mock<IPublicadorNotificacionEventos>();
            publicador
                .Setup(p => p.Notificar(It.IsAny<NotificacionEventoLanzado>()))
                .Throws(new InvalidOperationException("fallo notificando"));
            RegistrarEvento registrarEvento = CrearRegistrar(
                colaEventos.Object,
                colaEventosMemoria.Object,
                publicador.Object);
            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            Exception? excepcion = await Record.ExceptionAsync(
                () => registrarEvento.RegistrarEnColaAsync());

            Assert.Null(excepcion);
            colaEventosMemoria.Verify(
                c => c.EncolarAsync(It.IsAny<EventoOutbox>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static RegistrarEvento CrearRegistrar(
            IColaEventos colaEventos,
            IColaEventosMemoria colaEventosMemoria,
            IPublicadorNotificacionEventos publicador)
        {
            return new RegistrarEvento(
                colaEventos,
                colaEventosMemoria,
                publicador,
                NullLogger<RegistrarEvento>.Instance);
        }
    }
}
