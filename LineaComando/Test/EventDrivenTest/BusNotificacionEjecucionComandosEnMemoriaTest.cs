using Microsoft.Extensions.Logging.Abstractions;
using PER.Comandos.LineaComandos.Cola.Notificaciones;
using PER.Comandos.LineaComandos.EventDriven.Bus;

namespace EventDrivenTest
{
    public class BusNotificacionEjecucionComandosEnMemoriaTest
    {
        [Fact]
        public void Suscribir_ConRutaVacia_DebeLanzarExcepcion()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();

            Assert.Throws<ArgumentException>(() => bus.Suscribir(" "));
        }

        [Fact]
        public async Task Notificar_DebeEntregarSnapshotSoloALaRutaExacta()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEjecucionComando exacto = bus.Suscribir("pedido consultar");
            using IObservadorNotificacionEjecucionComando diferente = bus.Suscribir("Pedido Consultar");
            bool recibidoDiferente = false;
            diferente.NotificacionRecibida += (_, _) =>
            {
                recibidoDiferente = true;
                return Task.CompletedTask;
            };
            NotificacionEjecucionComando esperada = CrearNotificacion(
                NotificacionEjecucionComandoTipo.Iniciada);

            bus.Notificar(esperada);

            NotificacionEjecucionComando actual = await exacto.EsperarAsync(
                TestContext.Current.CancellationToken);
            await Task.Delay(25, TestContext.Current.CancellationToken);

            Assert.Same(esperada, actual);
            Assert.Equal(501, actual.ComandoId);
            Assert.Equal("pedido consultar", actual.RutaComando);
            Assert.False(recibidoDiferente);
        }

        [Fact]
        public async Task Notificar_ConVariosObservadores_DebeEntregarACadaUno()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEjecucionComando primero = bus.Suscribir("pedido consultar");
            using IObservadorNotificacionEjecucionComando segundo = bus.Suscribir("pedido consultar");
            NotificacionEjecucionComando notificacion = CrearNotificacion(
                NotificacionEjecucionComandoTipo.Iniciada);

            bus.Notificar(notificacion);

            Assert.Same(notificacion, await primero.EsperarAsync(TestContext.Current.CancellationToken));
            Assert.Same(notificacion, await segundo.EsperarAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task EsperarAsync_DebeConservarFIFOYPermitirReutilizarObservador()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEjecucionComando observador = bus.Suscribir("pedido consultar");
            NotificacionEjecucionComando iniciada = CrearNotificacion(
                NotificacionEjecucionComandoTipo.Iniciada);
            NotificacionEjecucionComando completada = CrearNotificacion(
                NotificacionEjecucionComandoTipo.Completada);

            bus.Notificar(iniciada);
            bus.Notificar(completada);

            Assert.Same(iniciada, await observador);
            Assert.Same(completada, await observador);
        }

        [Fact]
        public async Task CallbackLento_NoDebeBloquearAOtroObservadorNiAlPublicador()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEjecucionComando lento = bus.Suscribir("pedido consultar");
            using IObservadorNotificacionEjecucionComando rapido = bus.Suscribir("pedido consultar");
            TaskCompletionSource<bool> callbackIniciado = CrearFuente();
            TaskCompletionSource<bool> liberarCallback = CrearFuente();
            lento.NotificacionRecibida += async (_, token) =>
            {
                callbackIniciado.TrySetResult(true);
                await liberarCallback.Task.WaitAsync(token);
            };
            NotificacionEjecucionComando notificacion = CrearNotificacion(
                NotificacionEjecucionComandoTipo.Iniciada);

            bus.Notificar(notificacion);

            await callbackIniciado.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Same(
                notificacion,
                await rapido.EsperarAsync(TestContext.Current.CancellationToken));
            liberarCallback.TrySetResult(true);
        }

        [Fact]
        public async Task Observador_NoDebePermitirCombinarCallbackYEspera()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEjecucionComando observador = bus.Suscribir("pedido consultar");
            observador.NotificacionRecibida += (_, _) => Task.CompletedTask;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => observador.EsperarAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task CancelarEspera_NoDebeEliminarSuscripcion()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEjecucionComando observador = bus.Suscribir("pedido consultar");
            using CancellationTokenSource cancelacion = new CancellationTokenSource();
            Task<NotificacionEjecucionComando> esperaCancelada = observador.EsperarAsync(cancelacion.Token);

            cancelacion.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => esperaCancelada);

            NotificacionEjecucionComando notificacion = CrearNotificacion(
                NotificacionEjecucionComandoTipo.Completada);
            bus.Notificar(notificacion);

            Assert.Same(
                notificacion,
                await observador.EsperarAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Dispose_DebeLiberarEsperaYSerIdempotente()
        {
            BusNotificacionEjecucionComandosEnMemoria bus = CrearBus();
            IObservadorNotificacionEjecucionComando observador = bus.Suscribir("pedido consultar");
            Task<NotificacionEjecucionComando> espera = observador.EsperarAsync(
                TestContext.Current.CancellationToken);

            observador.Dispose();
            observador.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => espera);
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => observador.EsperarAsync(TestContext.Current.CancellationToken));
        }

        private static BusNotificacionEjecucionComandosEnMemoria CrearBus()
        {
            return new BusNotificacionEjecucionComandosEnMemoria(NullLoggerFactory.Instance);
        }

        private static NotificacionEjecucionComando CrearNotificacion(
            NotificacionEjecucionComandoTipo tipo)
        {
            return new NotificacionEjecucionComando(
                Guid.NewGuid(),
                501,
                "pedido consultar",
                tipo,
                OrigenEjecucionComandoTipo.Evento,
                "pedido.creado",
                77,
                new DateTime(2026, 8, 11, 15, 0, 0, DateTimeKind.Utc),
                tipo == NotificacionEjecucionComandoTipo.Iniciada
                    ? null
                    : TimeSpan.FromSeconds(1),
                null);
        }

        private static TaskCompletionSource<bool> CrearFuente()
        {
            return new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
