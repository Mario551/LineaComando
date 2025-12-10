using PER.Comandos.LineaComandos.EventDriven.Bus;

namespace EventDrivenTest
{
    public class BusEventosEnMemoriaTest
    {
        private class EventoPrueba
        {
            public string Mensaje { get; set; } = string.Empty;
            public int Valor { get; set; }
        }

        private class OtroEventoPrueba
        {
            public string Dato { get; set; } = string.Empty;
        }

        [Fact]
        public async Task PublicarAsync_SinSuscriptores_NoDebeLanzarExcepcion()
        {
            var bus = new BusEventosEnMemoria();
            var evento = new EventoPrueba { Mensaje = "Test", Valor = 42 };

            var exception = await Record.ExceptionAsync(() => bus.PublicarAsync(evento));

            Assert.Null(exception);
        }

        [Fact]
        public async Task PublicarAsync_ConSuscriptor_DebeInvocarManejador()
        {
            var bus = new BusEventosEnMemoria();
            bool eventoRecibido = false;
            EventoPrueba? eventoCapturado = null;

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                eventoRecibido = true;
                eventoCapturado = evento;
                return Task.CompletedTask;
            });

            var eventoEnviado = new EventoPrueba { Mensaje = "Hola", Valor = 100 };
            await bus.PublicarAsync(eventoEnviado);

            Assert.True(eventoRecibido);
            Assert.NotNull(eventoCapturado);
            Assert.Equal("Hola", eventoCapturado.Mensaje);
            Assert.Equal(100, eventoCapturado.Valor);
        }

        [Fact]
        public async Task PublicarAsync_ConMultiplesSuscriptores_DebeInvocarTodos()
        {
            var bus = new BusEventosEnMemoria();
            var contadorInvocaciones = 0;

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                Interlocked.Increment(ref contadorInvocaciones);
                return Task.CompletedTask;
            });

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                Interlocked.Increment(ref contadorInvocaciones);
                return Task.CompletedTask;
            });

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                Interlocked.Increment(ref contadorInvocaciones);
                return Task.CompletedTask;
            });

            await bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" });

            Assert.Equal(3, contadorInvocaciones);
        }

        [Fact]
        public async Task PublicarAsync_DiferentesTiposEvento_SoloInvocaSuscriptoresDelTipo()
        {
            var bus = new BusEventosEnMemoria();
            var contadorEventoPrueba = 0;
            var contadorOtroEvento = 0;

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                Interlocked.Increment(ref contadorEventoPrueba);
                return Task.CompletedTask;
            });

            bus.Suscribir<OtroEventoPrueba>((evento, token) =>
            {
                Interlocked.Increment(ref contadorOtroEvento);
                return Task.CompletedTask;
            });

            await bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" });

            Assert.Equal(1, contadorEventoPrueba);
            Assert.Equal(0, contadorOtroEvento);
        }

        [Fact]
        public void Suscribir_DebeAgregarManejador()
        {
            var bus = new BusEventosEnMemoria();
            bool manejadorAgregado = false;

            Func<EventoPrueba, CancellationToken, Task> manejador = (evento, token) =>
            {
                manejadorAgregado = true;
                return Task.CompletedTask;
            };

            var exception = Record.Exception(() => bus.Suscribir(manejador));

            Assert.False(manejadorAgregado);
            Assert.Null(exception);
        }

        [Fact]
        public async Task CancelarSuscripcion_DebeRemoverManejador()
        {
            var bus = new BusEventosEnMemoria();
            var invocado = false;

            Func<EventoPrueba, CancellationToken, Task> manejador = (evento, token) =>
            {
                invocado = true;
                return Task.CompletedTask;
            };

            bus.Suscribir(manejador);
            bus.CancelarSuscripcion(manejador);

            await bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" });

            Assert.False(invocado);
        }

        [Fact]
        public async Task CancelarSuscripcion_SoloRemueveSuscriptorEspecifico()
        {
            var bus = new BusEventosEnMemoria();
            var invocadoManejador1 = false;
            var invocadoManejador2 = false;

            Func<EventoPrueba, CancellationToken, Task> manejador1 = (evento, token) =>
            {
                invocadoManejador1 = true;
                return Task.CompletedTask;
            };

            Func<EventoPrueba, CancellationToken, Task> manejador2 = (evento, token) =>
            {
                invocadoManejador2 = true;
                return Task.CompletedTask;
            };

            bus.Suscribir(manejador1);
            bus.Suscribir(manejador2);
            bus.CancelarSuscripcion(manejador1);

            await bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" });

            Assert.False(invocadoManejador1);
            Assert.True(invocadoManejador2);
        }

        [Fact]
        public async Task PublicarAsync_ConCancelacion_DebeRespetarToken()
        {
            var bus = new BusEventosEnMemoria();
            var contadorInvocaciones = 0;

            bus.Suscribir<EventoPrueba>(async (evento, token) =>
            {
                Interlocked.Increment(ref contadorInvocaciones);
                await Task.Delay(100, token);
            });

            bus.Suscribir<EventoPrueba>(async (evento, token) =>
            {
                Interlocked.Increment(ref contadorInvocaciones);
                await Task.Delay(100, token);
            });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" }, cts.Token);

            Assert.Equal(0, contadorInvocaciones);
        }

        [Fact]
        public async Task PublicarAsync_ManejadorLanzaExcepcion_DeberiaPropagarla()
        {
            var bus = new BusEventosEnMemoria();

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                throw new InvalidOperationException("Error de prueba");
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" }));
        }

        [Fact]
        public async Task PublicarAsync_EsThreadSafe()
        {
            var bus = new BusEventosEnMemoria();
            var contadorInvocaciones = 0;

            bus.Suscribir<EventoPrueba>((evento, token) =>
            {
                Interlocked.Increment(ref contadorInvocaciones);
                return Task.CompletedTask;
            });

            var tareas = Enumerable.Range(0, 100)
                .Select(_ => bus.PublicarAsync(new EventoPrueba { Mensaje = "Test" }))
                .ToArray();

            await Task.WhenAll(tareas);

            Assert.Equal(100, contadorInvocaciones);
        }

        [Fact]
        public void CancelarSuscripcion_SinSuscripcionPrevia_NoDebeLanzarExcepcion()
        {
            var bus = new BusEventosEnMemoria();

            Func<EventoPrueba, CancellationToken, Task> manejador = (evento, token) => Task.CompletedTask;

            var exception = Record.Exception(() => bus.CancelarSuscripcion(manejador));

            Assert.Null(exception);
        }
    }
}
