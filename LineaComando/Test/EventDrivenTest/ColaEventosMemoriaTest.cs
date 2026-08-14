using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PER.Comandos.LineaComandos.EventDriven.Bus;
using PER.Comandos.LineaComandos.EventDriven.Colas;
using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace EventDrivenTest
{
    public class ColaEventosMemoriaTest
    {
        [Fact]
        public async Task EncolarAsync_DebeTransmitirEventoPorLectura()
        {
            ColaEventosMemoria cola = new ColaEventosMemoria(
                new ScopeFactoryPrueba(new ServiceProviderPrueba()));

            EventoOutbox evento = new EventoOutbox
            {
                Id = 10,
                CodigoTipoEvento = "pedido_creado",
                DatosEvento = "{\"pedidoId\":123}"
            };

            await cola.EncolarAsync(evento);

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<EventoOutbox> enumerador = cola.LeerAsync(cts.Token).GetAsyncEnumerator();

            Assert.True(await enumerador.MoveNextAsync());
            Assert.Equal(10, enumerador.Current.Id);
            Assert.Equal("pedido_creado", enumerador.Current.CodigoTipoEvento);
        }

        [Fact]
        public async Task CargarPendientesDesdeBaseDatosAsync_DebeEncolarPendientesEnMemoria()
        {
            EventoOutbox primerEvento = new EventoOutbox
            {
                Id = 10,
                CodigoTipoEvento = "pedido_creado",
                DatosEvento = "{\"pedidoId\":123}"
            };

            EventoOutbox segundoEvento = new EventoOutbox
            {
                Id = 11,
                CodigoTipoEvento = "pedido_pagado",
                DatosEvento = "{\"pedidoId\":123}"
            };

            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.ObtenerEventosPendientesAsync(int.MaxValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { primerEvento, segundoEvento });

            ColaEventosMemoria cola = new ColaEventosMemoria(
                new ScopeFactoryPrueba(new ServiceProviderPrueba(colaEventos.Object)));

            await cola.CargarPendientesDesdeBaseDatosAsync();

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<EventoOutbox> enumerador = cola.LeerAsync(cts.Token).GetAsyncEnumerator();

            Assert.True(await enumerador.MoveNextAsync());
            Assert.Equal(10, enumerador.Current.Id);

            Assert.True(await enumerador.MoveNextAsync());
            Assert.Equal(11, enumerador.Current.Id);
        }

        [Fact]
        public async Task CargaYReencoladoDirecto_NoDebenNotificarObservadores()
        {
            EventoOutbox evento = new EventoOutbox
            {
                Id = 10,
                CodigoTipoEvento = "pedido_creado",
                DatosEvento = "{\"pedidoId\":123}"
            };
            Mock<IColaEventos> colaEventos = new Mock<IColaEventos>();
            colaEventos
                .Setup(c => c.ObtenerEventosPendientesAsync(
                    int.MaxValue,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { evento });
            ColaEventosMemoria cola = new ColaEventosMemoria(
                new ScopeFactoryPrueba(new ServiceProviderPrueba(colaEventos.Object)));
            BusNotificacionEventosEnMemoria bus = new BusNotificacionEventosEnMemoria(
                NullLoggerFactory.Instance);
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido_creado");
            using CancellationTokenSource cancelacion = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(100));

            await cola.CargarPendientesDesdeBaseDatosAsync(TestContext.Current.CancellationToken);
            await cola.EncolarAsync(evento, TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => observador.EsperarAsync(cancelacion.Token));
        }

        private sealed class ScopeFactoryPrueba : IServiceScopeFactory
        {
            private readonly IServiceProvider _serviceProvider;

            public ScopeFactoryPrueba(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public IServiceScope CreateScope()
            {
                return new ScopePrueba(_serviceProvider);
            }
        }

        private sealed class ScopePrueba : IServiceScope
        {
            public ScopePrueba(IServiceProvider serviceProvider)
            {
                ServiceProvider = serviceProvider;
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose()
            {
            }
        }

        private sealed class ServiceProviderPrueba : IServiceProvider
        {
            private readonly IColaEventos? _colaEventos;

            public ServiceProviderPrueba(IColaEventos? colaEventos = null)
            {
                _colaEventos = colaEventos;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IColaEventos))
                    return _colaEventos;

                return null;
            }
        }
    }
}
