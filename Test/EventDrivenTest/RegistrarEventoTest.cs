using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PER.Comandos.LineaComandos.EventDriven.Colas;
using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace EventDrivenTest
{
    public class RegistrarEventoTest
    {
        [Fact]
        public async Task RegistrarEnColaAsync_DebeGuardarEventoAntesDeEncolarEnMemoria()
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

            RegistrarEvento registrarEvento = new RegistrarEvento(
                colaEventos.Object,
                colaEventosMemoria.Object,
                NullLogger<RegistrarEvento>.Instance);

            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            await registrarEvento.RegistrarEnColaAsync();

            Assert.Equal(new[] { "persistido", "encolado" }, orden);
        }

        [Fact]
        public async Task RegistrarEnColaAsync_SiFallaPersistencia_NoDebeEncolarEnMemoria()
        {
            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.GuardarEventoAsync(It.IsAny<DatosEvento>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("fallo persistiendo"));

            Mock<IColaEventosMemoria> colaEventosMemoria = new Mock<IColaEventosMemoria>();

            RegistrarEvento registrarEvento = new RegistrarEvento(
                colaEventos.Object,
                colaEventosMemoria.Object,
                NullLogger<RegistrarEvento>.Instance);

            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            await Assert.ThrowsAsync<InvalidOperationException>(() => registrarEvento.RegistrarEnColaAsync());

            colaEventosMemoria.Verify(
                c => c.EncolarAsync(It.IsAny<EventoOutbox>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RegistrarEnColaAsync_SiFallaEncoladoEnMemoria_NoDebeFallar()
        {
            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.GuardarEventoAsync(It.IsAny<DatosEvento>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(15);

            Mock<IColaEventosMemoria> colaEventosMemoria = new Mock<IColaEventosMemoria>();
            colaEventosMemoria
                .Setup(c => c.EncolarAsync(It.IsAny<EventoOutbox>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("fallo en memoria"));

            RegistrarEvento registrarEvento = new RegistrarEvento(
                colaEventos.Object,
                colaEventosMemoria.Object,
                NullLogger<RegistrarEvento>.Instance);

            registrarEvento.Argumentos("pedido_creado", new { PedidoId = 123 }, 77);

            await registrarEvento.RegistrarEnColaAsync();

            colaEventos.Verify(
                c => c.GuardarEventoAsync(It.IsAny<DatosEvento>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
