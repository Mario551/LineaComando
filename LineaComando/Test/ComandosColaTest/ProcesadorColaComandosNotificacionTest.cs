using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Notificaciones;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosColaTest
{
    public class ProcesadorColaComandosNotificacionTest
    {
        [Fact]
        public async Task EjecutarComandoExitoso_DebeNotificarInicioYFinEnOrden()
        {
            ComandoEnCola comando = CrearComando(7001);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Exito("ok")));

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                new[]
                {
                    "procesando:7001",
                    "notificar:Iniciada:7001",
                    "ejecutar",
                    "persistido:7001",
                    "notificar:Completada:7001",
                    "entregado:7001"
                },
                escenario.Orden);
            NotificacionEjecucionComando[] notificaciones = escenario.Notificaciones.ToArray();
            Assert.Equal(2, notificaciones.Length);
            Assert.Equal(notificaciones[0].EjecucionId, notificaciones[1].EjecucionId);
            Assert.Equal(NotificacionEjecucionComandoTipo.Iniciada, notificaciones[0].Tipo);
            Assert.Equal(NotificacionEjecucionComandoTipo.Completada, notificaciones[1].Tipo);
            Assert.Equal(OrigenEjecucionComandoTipo.Directo, notificaciones[0].Origen);
            Assert.Null(notificaciones[0].Duracion);
            Assert.NotNull(notificaciones[1].Duracion);
            Assert.Null(notificaciones[1].Error);
        }

        [Fact]
        public async Task EjecutarComandoConFalloFuncional_DebeNotificarFallidaDespuesDePersistir()
        {
            ComandoEnCola comando = CrearComando(7002);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Fallo("fallo funcional")));

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            NotificacionEjecucionComando terminal = Assert.Single(
                escenario.Notificaciones,
                notificacion => notificacion.Tipo == NotificacionEjecucionComandoTipo.Fallida);
            Assert.Equal("fallo funcional", terminal.Error);
            string[] orden = escenario.Orden.ToArray();
            Assert.True(
                Array.IndexOf(orden, "persistido:7002") <
                Array.IndexOf(orden, "notificar:Fallida:7002"));
        }

        [Fact]
        public async Task EjecutarComandoConExcepcion_DebePersistirYNotificarFallo()
        {
            ComandoEnCola comando = CrearComando(7003);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                _ => throw new InvalidOperationException("error de ejecución"));

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            NotificacionEjecucionComando terminal = Assert.Single(
                escenario.Notificaciones,
                notificacion => notificacion.Tipo == NotificacionEjecucionComandoTipo.Fallida);
            Assert.Contains("error de ejecución", terminal.Error);
            escenario.Almacen.Verify(
                almacen => almacen.MarcarComoProcesadoAsync(
                    comando.Id,
                    It.Is<ResultadoComando>(resultado => !resultado.Exitoso),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelarComandoIniciado_DebeNotificarInterrupcionSinPersistirTerminal()
        {
            ComandoEnCola comando = CrearComando(7004);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                async token =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return ResultadoComando.Exito();
                });
            using CancellationTokenSource cancelacion = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(50));

            await escenario.Procesador.StartAsync(cancelacion.Token);

            Assert.Equal(
                new[]
                {
                    NotificacionEjecucionComandoTipo.Iniciada,
                    NotificacionEjecucionComandoTipo.Interrumpida
                },
                escenario.Notificaciones.Select(notificacion => notificacion.Tipo));
            escenario.Almacen.Verify(
                almacen => almacen.MarcarComoProcesadoAsync(
                    It.IsAny<long>(),
                    It.IsAny<ResultadoComando>(),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            escenario.Cola.Verify(
                cola => cola.CompletarResultado(
                    It.IsAny<long>(),
                    It.IsAny<ResultadoComando>()),
                Times.Never);
        }

        [Fact]
        public async Task FallarPersistenciaTerminal_DebeNotificarErrorPersistencia()
        {
            ComandoEnCola comando = CrearComando(7005);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Exito()),
                fallarPersistencia: true);

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                new[]
                {
                    NotificacionEjecucionComandoTipo.Iniciada,
                    NotificacionEjecucionComandoTipo.ErrorPersistencia
                },
                escenario.Notificaciones.Select(notificacion => notificacion.Tipo));
            NotificacionEjecucionComando error = escenario.Notificaciones.Last();
            Assert.Contains("fallo de persistencia", error.Error);
            escenario.Cola.Verify(
                cola => cola.CompletarResultado(
                    comando.Id,
                    It.Is<ResultadoComando>(resultado => !resultado.Exitoso)),
                Times.Once);
        }

        [Theory]
        [InlineData(null, OrigenEjecucionComandoTipo.Directo, null, null)]
        [InlineData("--dato=1", OrigenEjecucionComandoTipo.Directo, null, null)]
        [InlineData("--origen=evento --codigo=pedido.creado --agregado-id=77", OrigenEjecucionComandoTipo.Evento, "pedido.creado", 77L)]
        [InlineData("--origen=disparador --codigo=cierre-diario", OrigenEjecucionComandoTipo.Disparador, "cierre-diario", null)]
        [InlineData("--origen=otro --codigo=desconocido", OrigenEjecucionComandoTipo.Desconocido, "desconocido", null)]
        [InlineData("--origen=evento --codigo=pedido.creado --agregado-id=invalido", OrigenEjecucionComandoTipo.Desconocido, "pedido.creado", null)]
        public async Task EjecutarComando_DebeIdentificarOrigenTecnico(
            string? argumentos,
            OrigenEjecucionComandoTipo origenEsperado,
            string? codigoEsperado,
            long? agregadoEsperado)
        {
            ComandoEnCola comando = CrearComando(7006, argumentos);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Exito()));

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            NotificacionEjecucionComando iniciada = escenario.Notificaciones.First();
            Assert.Equal(origenEsperado, iniciada.Origen);
            Assert.Equal(codigoEsperado, iniciada.CodigoOrigen);
            Assert.Equal(agregadoEsperado, iniciada.AgregadoId);
        }

        [Fact]
        public async Task FallarPublicador_NoDebeAfectarPersistenciaNiResultado()
        {
            ComandoEnCola comando = CrearComando(7007);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Exito()),
                fallarPublicador: true);

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            escenario.Almacen.Verify(
                almacen => almacen.MarcarComoProcesadoAsync(
                    comando.Id,
                    It.Is<ResultadoComando>(resultado => resultado.Exitoso),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            escenario.Cola.Verify(
                cola => cola.CompletarResultado(
                    comando.Id,
                    It.Is<ResultadoComando>(resultado => resultado.Exitoso)),
                Times.Once);
        }

        [Fact]
        public async Task ReejecutarMismoComando_DebeGenerarNuevoIdentificadorDeEjecucion()
        {
            ComandoEnCola comando = CrearComando(7008);
            using EscenarioProcesador primera = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Exito()));
            using EscenarioProcesador segunda = CrearEscenario(
                new[] { comando },
                _ => Task.FromResult(ResultadoComando.Exito()));

            await primera.Procesador.StartAsync(TestContext.Current.CancellationToken);
            await segunda.Procesador.StartAsync(TestContext.Current.CancellationToken);

            Guid primeraEjecucion = primera.Notificaciones.First().EjecucionId;
            Guid segundaEjecucion = segunda.Notificaciones.First().EjecucionId;
            Assert.NotEqual(primeraEjecucion, segundaEjecucion);
        }

        [Fact]
        public async Task EjecutarComandosConcurrentes_DebeCorrelacionarCadaParDeNotificaciones()
        {
            ComandoEnCola primero = CrearComando(7009);
            ComandoEnCola segundo = CrearComando(7010);
            using EscenarioProcesador escenario = CrearEscenario(
                new[] { primero, segundo },
                async token =>
                {
                    await Task.Delay(20, token);
                    return ResultadoComando.Exito();
                },
                maxParalelismo: 2);

            await escenario.Procesador.StartAsync(TestContext.Current.CancellationToken);

            IGrouping<long, NotificacionEjecucionComando>[] grupos = escenario.Notificaciones
                .GroupBy(notificacion => notificacion.ComandoId)
                .ToArray();
            Assert.Equal(2, grupos.Length);
            Assert.All(grupos, grupo =>
            {
                NotificacionEjecucionComando[] notificaciones = grupo.ToArray();
                Assert.Equal(2, notificaciones.Length);
                Assert.Equal(notificaciones[0].EjecucionId, notificaciones[1].EjecucionId);
            });
            Assert.NotEqual(
                grupos[0].First().EjecucionId,
                grupos[1].First().EjecucionId);
        }

        [Fact]
        public void Constructor_ConPublicadorNulo_DebeLanzarExcepcion()
        {
            ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
            Mock<IColaComandosMemoria> cola = new Mock<IColaComandosMemoria>();

            Assert.Throws<ArgumentNullException>(() => new ProcesadorColaComandos(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                cola.Object,
                null!,
                1,
                NullLogger<ProcesadorColaComandos>.Instance));
        }

        private static EscenarioProcesador CrearEscenario(
            IReadOnlyList<ComandoEnCola> comandos,
            Func<CancellationToken, Task<ResultadoComando>> ejecutar,
            bool fallarPersistencia = false,
            bool fallarPublicador = false,
            int maxParalelismo = 1)
        {
            ConcurrentQueue<string> orden = new ConcurrentQueue<string>();
            ConcurrentQueue<NotificacionEjecucionComando> notificaciones =
                new ConcurrentQueue<NotificacionEjecucionComando>();
            Dictionary<long, ComandoEnCola> comandosPorId = comandos.ToDictionary(comando => comando.Id);
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.MarcarComandosProcesandoAsync(
                    It.IsAny<long[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((long[] ids, CancellationToken _) =>
                {
                    foreach (long id in ids)
                        orden.Enqueue($"procesando:{id}");

                    return ids.Select(id => comandosPorId[id]);
                });
            almacen
                .Setup(a => a.MarcarComoProcesadoAsync(
                    It.IsAny<long>(),
                    It.IsAny<ResultadoComando>(),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((long id, ResultadoComando _, PayloadResultadoComando? _, CancellationToken _) =>
                {
                    orden.Enqueue($"persistido:{id}");

                    if (fallarPersistencia)
                        throw new InvalidOperationException("fallo de persistencia");

                    return Task.CompletedTask;
                });

            ComandoCicloVidaPrueba comandoPrueba = new ComandoCicloVidaPrueba(async token =>
            {
                orden.Enqueue("ejecutar");
                return await ejecutar(token);
            });
            FactoriaComandos<string, ResultadoComando> factoria =
                new FactoriaComandos<string, ResultadoComando>();
            factoria
                .Add("test")
                .Add("ciclo", new Nodo<string, ResultadoComando>(comandoPrueba));

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton<IFactoriaComandos<string, ResultadoComando>>(factoria)
                .BuildServiceProvider();
            Mock<IColaComandosMemoria> cola = new Mock<IColaComandosMemoria>();
            cola
                .Setup(c => c.CargarPendientesDesdeBaseDatosAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            cola
                .Setup(c => c.LeerAsync(It.IsAny<CancellationToken>()))
                .Returns(LeerComandosAsync(comandos));
            cola
                .Setup(c => c.CompletarResultado(
                    It.IsAny<long>(),
                    It.IsAny<ResultadoComando>()))
                .Callback<long, ResultadoComando>((id, _) => orden.Enqueue($"entregado:{id}"));

            Mock<IPublicadorNotificacionEjecucionComandos> publicador =
                new Mock<IPublicadorNotificacionEjecucionComandos>();
            publicador
                .Setup(p => p.Notificar(It.IsAny<NotificacionEjecucionComando>()))
                .Callback<NotificacionEjecucionComando>(notificacion =>
                {
                    if (fallarPublicador)
                        throw new InvalidOperationException("fallo del publicador");

                    notificaciones.Enqueue(notificacion);
                    orden.Enqueue($"notificar:{notificacion.Tipo}:{notificacion.ComandoId}");
                });

            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                cola.Object,
                publicador.Object,
                maxParalelismo,
                NullLogger<ProcesadorColaComandos>.Instance);

            return new EscenarioProcesador(
                procesador,
                almacen,
                cola,
                notificaciones,
                orden,
                serviceProvider);
        }

        private static ComandoEnCola CrearComando(long id, string? argumentos = null)
        {
            return new ComandoEnCola
            {
                Id = id,
                RutaComando = "test ciclo",
                Argumentos = argumentos ?? string.Empty,
                DatosDeComando = "{}",
                FechaCreacion = DateTime.UtcNow,
                Estado = "pendiente",
                Intentos = 0
            };
        }

        private static async IAsyncEnumerable<ComandoEnCola> LeerComandosAsync(
            IEnumerable<ComandoEnCola> comandos)
        {
            foreach (ComandoEnCola comando in comandos)
            {
                await Task.Yield();
                yield return comando;
            }
        }

        private sealed class ComandoCicloVidaPrueba : ComandoBase<string, ResultadoComando>
        {
            private readonly Func<CancellationToken, Task<ResultadoComando>> _ejecutar;

            public ComandoCicloVidaPrueba(
                Func<CancellationToken, Task<ResultadoComando>> ejecutar)
            {
                _ejecutar = ejecutar;
            }

            public override void Preparar(ICollection<Parametro> parametros)
            {
            }

            public override Task<ResultadoComando> EjecutarAsync(
                string entrada,
                CancellationToken token = default)
            {
                return _ejecutar(token);
            }
        }

        private sealed class EscenarioProcesador : IDisposable
        {
            private readonly ServiceProvider _serviceProvider;

            public ProcesadorColaComandos Procesador { get; }
            public Mock<IAlmacenColaComandos> Almacen { get; }
            public Mock<IColaComandosMemoria> Cola { get; }
            public ConcurrentQueue<NotificacionEjecucionComando> Notificaciones { get; }
            public ConcurrentQueue<string> Orden { get; }

            public EscenarioProcesador(
                ProcesadorColaComandos procesador,
                Mock<IAlmacenColaComandos> almacen,
                Mock<IColaComandosMemoria> cola,
                ConcurrentQueue<NotificacionEjecucionComando> notificaciones,
                ConcurrentQueue<string> orden,
                ServiceProvider serviceProvider)
            {
                Procesador = procesador;
                Almacen = almacen;
                Cola = cola;
                Notificaciones = notificaciones;
                Orden = orden;
                _serviceProvider = serviceProvider;
            }

            public void Dispose()
            {
                _serviceProvider.Dispose();
            }
        }
    }
}
