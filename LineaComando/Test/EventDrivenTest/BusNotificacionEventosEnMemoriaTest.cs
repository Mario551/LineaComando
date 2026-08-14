using Microsoft.Extensions.Logging.Abstractions;
using PER.Comandos.LineaComandos.EventDriven.Bus;

namespace EventDrivenTest
{
    public class BusNotificacionEventosEnMemoriaTest
    {
        [Fact]
        public void Suscribir_ConNombreVacio_DebeLanzarExcepcion()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();

            Assert.Throws<ArgumentException>(() => bus.Suscribir(" "));
        }

        [Fact]
        public void Notificar_SinObservadores_NoDebeLanzarExcepcion()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();

            Exception? excepcion = Record.Exception(() => bus.Notificar(CrearNotificacion(1)));

            Assert.Null(excepcion);
        }

        [Fact]
        public async Task EventoRecibido_DebeEntregarSnapshotCompleto()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            TaskCompletionSource<NotificacionEventoLanzado> recibido = CrearFuente();

            observador.EventoRecibido += (evento, _) =>
            {
                recibido.TrySetResult(evento);
                return Task.CompletedTask;
            };

            DateTime fecha = new DateTime(2026, 8, 11, 12, 30, 0, DateTimeKind.Utc);
            NotificacionEventoLanzado esperado = new NotificacionEventoLanzado(
                15,
                "pedido.creado",
                77,
                "{\"pedidoId\":123}",
                fecha);

            bus.Notificar(esperado);

            NotificacionEventoLanzado actual = await recibido.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Same(esperado, actual);
            Assert.Equal(15, actual.Id);
            Assert.Equal("pedido.creado", actual.NombreEvento);
            Assert.Equal(77, actual.AgregadoId);
            Assert.Equal("{\"pedidoId\":123}", actual.DatosEvento);
            Assert.Equal(fecha, actual.CreadoEn);
        }

        [Fact]
        public async Task Notificar_ConVariosObservadores_DebeEntregarACadaUno()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento primero = bus.Suscribir("pedido.creado");
            using IObservadorNotificacionEvento segundo = bus.Suscribir("pedido.creado");
            TaskCompletionSource<NotificacionEventoLanzado> recibidoPrimero = CrearFuente();
            TaskCompletionSource<NotificacionEventoLanzado> recibidoSegundo = CrearFuente();

            primero.EventoRecibido += (evento, _) =>
            {
                recibidoPrimero.TrySetResult(evento);
                return Task.CompletedTask;
            };
            segundo.EventoRecibido += (evento, _) =>
            {
                recibidoSegundo.TrySetResult(evento);
                return Task.CompletedTask;
            };

            NotificacionEventoLanzado notificacion = CrearNotificacion(1);
            bus.Notificar(notificacion);

            Assert.Same(
                notificacion,
                await recibidoPrimero.Task.WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken));
            Assert.Same(
                notificacion,
                await recibidoSegundo.Task.WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Notificar_DebeCompararNombreConOrdinalExacto()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento minuscula = bus.Suscribir("pedido.creado");
            using IObservadorNotificacionEvento mayuscula = bus.Suscribir("Pedido.Creado");
            bool recibidoMayuscula = false;

            mayuscula.EventoRecibido += (_, _) =>
            {
                recibidoMayuscula = true;
                return Task.CompletedTask;
            };

            bus.Notificar(CrearNotificacion(1));

            NotificacionEventoLanzado recibido = await minuscula.EsperarAsync(
                TestContext.Current.CancellationToken);
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.Equal(1, recibido.Id);
            Assert.False(recibidoMayuscula);
        }

        [Fact]
        public async Task CallbackConError_NoDebeDetenerCallbacksNiNotificacionesPosteriores()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            TaskCompletionSource<bool> completado = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int ejecucionesExitosas = 0;

            observador.EventoRecibido += (_, _) =>
                throw new InvalidOperationException("fallo esperado");
            observador.EventoRecibido += (_, _) =>
            {
                if (Interlocked.Increment(ref ejecucionesExitosas) == 2)
                    completado.TrySetResult(true);

                return Task.CompletedTask;
            };

            bus.Notificar(CrearNotificacion(1));
            bus.Notificar(CrearNotificacion(2));

            await completado.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Equal(2, ejecucionesExitosas);
        }

        [Fact]
        public async Task CallbacksAsincronos_DebenEjecutarseSecuencialmenteEnOrdenDeRegistro()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            List<string> orden = new List<string>();
            TaskCompletionSource<bool> completado = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            observador.EventoRecibido += async (_, token) =>
            {
                orden.Add("primero-inicio");
                await Task.Delay(25, token);
                orden.Add("primero-fin");
            };
            observador.EventoRecibido += (_, _) =>
            {
                orden.Add("segundo");
                completado.TrySetResult(true);
                return Task.CompletedTask;
            };

            bus.Notificar(CrearNotificacion(1));

            await completado.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Equal(
                new[] { "primero-inicio", "primero-fin", "segundo" },
                orden);
        }

        [Fact]
        public async Task CallbackRegistradoDespuesDeNotificar_DebeRecibirEventoAlmacenado()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            TaskCompletionSource<NotificacionEventoLanzado> recibido = CrearFuente();

            bus.Notificar(CrearNotificacion(1));

            observador.EventoRecibido += (evento, _) =>
            {
                recibido.TrySetResult(evento);
                return Task.CompletedTask;
            };

            NotificacionEventoLanzado notificacion = await recibido.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Equal(1, notificacion.Id);
        }

        [Fact]
        public async Task EsperarAsync_DebeBloquearHastaRecibirNotificacion()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");

            Task<NotificacionEventoLanzado> espera = observador.EsperarAsync(
                TestContext.Current.CancellationToken);
            await Task.Yield();
            Assert.False(espera.IsCompleted);

            bus.Notificar(CrearNotificacion(1));

            NotificacionEventoLanzado recibido = await espera.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            Assert.Equal(1, recibido.Id);
        }

        [Fact]
        public async Task EsperarAsync_DebeConservarNotificacionAnteriorAlPrimerAwait()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");

            bus.Notificar(CrearNotificacion(1));

            NotificacionEventoLanzado recibido = await observador.EsperarAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(1, recibido.Id);
        }

        [Fact]
        public async Task ObservadorAwaitable_DebePoderUsarseVariasVecesEnFIFO()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");

            bus.Notificar(CrearNotificacion(1));
            bus.Notificar(CrearNotificacion(2));

            NotificacionEventoLanzado primero = await observador;
            NotificacionEventoLanzado segundo = await observador;

            Assert.Equal(1, primero.Id);
            Assert.Equal(2, segundo.Id);
        }

        [Fact]
        public async Task EsperarAsync_ConEsperaConcurrente_DebeLanzarExcepcion()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");

            Task<NotificacionEventoLanzado> primeraEspera = observador.EsperarAsync(
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => observador.EsperarAsync(TestContext.Current.CancellationToken));

            bus.Notificar(CrearNotificacion(1));
            Assert.Equal(1, (await primeraEspera).Id);
        }

        [Fact]
        public async Task ObservadorEnModoCallback_NoDebePermitirAwait()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            observador.EventoRecibido += (_, _) => Task.CompletedTask;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => observador.EsperarAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObservadorEnModoEspera_NoDebePermitirCallback()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            using CancellationTokenSource cancelacion = new CancellationTokenSource();
            Task<NotificacionEventoLanzado> espera = observador.EsperarAsync(cancelacion.Token);
            Func<NotificacionEventoLanzado, CancellationToken, Task> callback =
                (_, _) => Task.CompletedTask;

            Assert.Throws<InvalidOperationException>(() =>
            {
                observador.EventoRecibido += callback;
            });

            cancelacion.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => espera);
        }

        [Fact]
        public async Task CancelarEspera_NoDebeEliminarObservador()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            using IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            using CancellationTokenSource cancelacion = new CancellationTokenSource();

            Task<NotificacionEventoLanzado> esperaCancelada =
                observador.EsperarAsync(cancelacion.Token);
            cancelacion.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => esperaCancelada);

            bus.Notificar(CrearNotificacion(2));
            NotificacionEventoLanzado recibido = await observador.EsperarAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(2, recibido.Id);
        }

        [Fact]
        public async Task Dispose_DebeLiberarEsperaPendienteYSerIdempotente()
        {
            BusNotificacionEventosEnMemoria bus = CrearBus();
            IObservadorNotificacionEvento observador = bus.Suscribir("pedido.creado");
            Task<NotificacionEventoLanzado> espera = observador.EsperarAsync(
                TestContext.Current.CancellationToken);

            observador.Dispose();
            observador.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => espera);
            bus.Notificar(CrearNotificacion(1));
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => observador.EsperarAsync(TestContext.Current.CancellationToken));
        }

        private static BusNotificacionEventosEnMemoria CrearBus()
        {
            return new BusNotificacionEventosEnMemoria(NullLoggerFactory.Instance);
        }

        private static NotificacionEventoLanzado CrearNotificacion(long id)
        {
            return new NotificacionEventoLanzado(
                id,
                "pedido.creado",
                77,
                "{\"pedidoId\":123}",
                new DateTime(2026, 8, 11, 12, 30, 0, DateTimeKind.Utc));
        }

        private static TaskCompletionSource<NotificacionEventoLanzado> CrearFuente()
        {
            return new TaskCompletionSource<NotificacionEventoLanzado>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
