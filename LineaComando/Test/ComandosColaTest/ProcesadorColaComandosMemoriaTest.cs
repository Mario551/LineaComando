using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ComandosColaTest.Helpers;
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
    public class ProcesadorColaComandosMemoriaTest
    {
        [Fact]
        public async Task StartAsync_DataEnCli_DebePersistirSeparadoYEntregarAlComando()
        {
            const string data = """{ "mensaje": "O'Connor --modo", "ruta": "C:\\tmp", "unicode": "\u0041" }""";
            const string argumentos = """--modo=validar --data='{ "mensaje": "O\'Connor --modo", "ruta": "C:\\\\tmp", "unicode": "\\u0041" }'""";
            ComandoEnCola? comandoPersistido = null;
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()))
                .Callback<ComandoEnCola, CancellationToken>((comando, _) => comandoPersistido = comando)
                .ReturnsAsync(9002);
            almacen
                .Setup(a => a.ObtenerComandosPendientesAsync(int.MaxValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            almacen
                .Setup(a => a.MarcarComandosProcesandoAsync(
                    It.Is<long[]>(ids => ids.SequenceEqual(new long[] { 9002 })),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new[] { comandoPersistido! });
            almacen
                .Setup(a => a.MarcarComoProcesadoAsync(
                    9002,
                    It.IsAny<ResultadoComando>(),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            ComandoCaptura comandoCaptura = new ComandoCaptura();
            FactoriaComandos<string, ResultadoComando> factoriaComandos = new("test");
            factoriaComandos.Add("data", new Nodo<string, ResultadoComando>(comandoCaptura));
            FactoriaAbstractaComandos<string, ResultadoComando> factoria = new([factoriaComandos]);
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton<IFactoriaAbstractaComandos<string, ResultadoComando>>(factoria)
                .BuildServiceProvider();
            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());
            ComandoEncolado comando = await cola.EncolarAsync(new SolicitudComando
            {
                RutaComando = "test data",
                Argumentos = argumentos
            }, TestContext.Current.CancellationToken);
            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                cola,
                Mock.Of<IPublicadorNotificacionEjecucionComandos>(),
                1,
                TimeSpan.FromSeconds(30),
                10,
                NullLogger<ProcesadorColaComandos>.Instance);
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            Task procesamiento = procesador.StartAsync(cts.Token);
            ResultadoComando resultado;
            try
            {
                resultado = await comando.Resultado.WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                cts.Cancel();
                await procesamiento.WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken);
            }

            Assert.NotNull(comandoPersistido);
            Assert.Equal("--modo=validar", comandoPersistido!.Argumentos);
            Assert.Equal(data, comandoPersistido.DatosDeComando);
            Parametro parametro = Assert.Single(comandoCaptura.Parametros);
            Assert.Equal("--modo", parametro.Nombre);
            Assert.Equal("validar", parametro.Valor);
            Assert.Equal(data, comandoCaptura.Entrada);
            Assert.True(resultado.Exitoso);
        }

        [Fact]
        public async Task StartAsync_DebePersistirResultadoAntesDeCompletarEspera()
        {
            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                Id = 9001,
                RutaComando = "test persistencia",
                Argumentos = string.Empty,
                DatosDeComando = "{}",
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            List<string> orden = new List<string>();

            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.MarcarComandosProcesandoAsync(
                    It.Is<long[]>(ids => ids.SequenceEqual(new[] { comandoEnCola.Id })),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { comandoEnCola });

            almacen
                .Setup(a => a.MarcarComoProcesadoAsync(
                    comandoEnCola.Id,
                    It.IsAny<ResultadoComando>(),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<long, ResultadoComando, PayloadResultadoComando?, CancellationToken>((_, resultado, _, _) =>
                {
                    Assert.True(resultado.Exitoso);
                    orden.Add("persistido");
                })
                .Returns(Task.CompletedTask);

            FactoriaComandos<string, ResultadoComando> factoriaComandos = new("test");
            factoriaComandos.Add(
                "persistencia",
                new Nodo<string, ResultadoComando>(new ComandoPrueba("ok")));
            FactoriaAbstractaComandos<string, ResultadoComando> factoria = new([factoriaComandos]);

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton<IFactoriaAbstractaComandos<string, ResultadoComando>>(factoria)
                .BuildServiceProvider();

            Mock<IColaComandosMemoria> colaComandosMemoria = new Mock<IColaComandosMemoria>();
            colaComandosMemoria
                .Setup(c => c.CargarPendientesDesdeBaseDatosAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            colaComandosMemoria
                .Setup(c => c.LeerAsync(It.IsAny<CancellationToken>()))
                .Returns(LeerComandosAsync(new[] { comandoEnCola }));
            colaComandosMemoria
                .Setup(c => c.CompletarResultado(comandoEnCola.Id, It.IsAny<ResultadoComando>()))
                .Callback<long, ResultadoComando>((_, resultado) =>
                {
                    Assert.True(resultado.Exitoso);
                    orden.Add("entregado");
                });

            Mock<IPublicadorNotificacionEjecucionComandos> publicadorNotificaciones =
                new Mock<IPublicadorNotificacionEjecucionComandos>();
            publicadorNotificaciones
                .Setup(p => p.Notificar(It.IsAny<NotificacionEjecucionComando>()))
                .Callback<NotificacionEjecucionComando>(notificacion =>
                {
                    orden.Add(notificacion.Tipo.ToString().ToLowerInvariant());
                });

            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                colaComandosMemoria.Object,
                publicadorNotificaciones.Object,
                1,
                TimeSpan.FromSeconds(30),
                10,
                NullLogger<ProcesadorColaComandos>.Instance);

            await procesador.StartAsync(CancellationToken.None);

            Assert.Equal(
                new[] { "iniciada", "persistido", "completada", "entregado" },
                orden);
        }

        [Fact]
        public async Task StartAsync_ConMismaRutaLocal_DebeEjecutarComandosDeDistintasFactorias()
        {
            ComandoEnCola comandoPedido = new()
            {
                Id = 9101,
                RutaComando = "pedido consultar",
                DatosDeComando = "pedido",
                FechaCreacion = DateTime.UtcNow,
                Estado = "pendiente"
            };
            ComandoEnCola comandoCliente = new()
            {
                Id = 9102,
                RutaComando = "cliente consultar",
                DatosDeComando = "cliente",
                FechaCreacion = DateTime.UtcNow,
                Estado = "pendiente"
            };
            Dictionary<long, ComandoEnCola> comandos = new()
            {
                [comandoPedido.Id] = comandoPedido,
                [comandoCliente.Id] = comandoCliente
            };
            Mock<IAlmacenColaComandos> almacen = new();
            almacen
                .Setup(a => a.MarcarComandosProcesandoAsync(
                    It.IsAny<long[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((long[] ids, CancellationToken _) =>
                    ids.Select(id => comandos[id]));
            almacen
                .Setup(a => a.MarcarComoProcesadoAsync(
                    It.IsAny<long>(),
                    It.IsAny<ResultadoComando>(),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            ComandoCaptura capturaPedido = new();
            ComandoCaptura capturaCliente = new();
            FactoriaComandos<string, ResultadoComando> pedidos = new("pedido");
            pedidos.Add("consultar", new Nodo<string, ResultadoComando>(capturaPedido));
            FactoriaComandos<string, ResultadoComando> clientes = new("cliente");
            clientes.Add("consultar", new Nodo<string, ResultadoComando>(capturaCliente));
            FactoriaAbstractaComandos<string, ResultadoComando> factoria = new([pedidos, clientes]);
            using ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton<IFactoriaAbstractaComandos<string, ResultadoComando>>(factoria)
                .BuildServiceProvider();
            Mock<IColaComandosMemoria> cola = new();
            cola
                .Setup(c => c.CargarPendientesDesdeBaseDatosAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            cola
                .Setup(c => c.LeerAsync(It.IsAny<CancellationToken>()))
                .Returns(LeerComandosAsync([comandoPedido, comandoCliente]));
            ProcesadorColaComandos procesador = new(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                cola.Object,
                Mock.Of<IPublicadorNotificacionEjecucionComandos>(),
                2,
                TimeSpan.FromSeconds(30),
                10,
                NullLogger<ProcesadorColaComandos>.Instance);

            await procesador.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal("pedido", capturaPedido.Entrada);
            Assert.Equal("cliente", capturaCliente.Entrada);
            almacen.Verify(
                a => a.MarcarComoProcesadoAsync(
                    It.IsAny<long>(),
                    It.Is<ResultadoComando>(resultado => resultado.Exitoso),
                    It.IsAny<PayloadResultadoComando?>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        private static async IAsyncEnumerable<ComandoEnCola> LeerComandosAsync(IEnumerable<ComandoEnCola> comandos)
        {
            foreach (ComandoEnCola comando in comandos)
            {
                await Task.Yield();
                yield return comando;
            }
        }

        private sealed class ComandoCaptura : ComandoBase<string, ResultadoComando>
        {
            public ICollection<Parametro> Parametros { get; private set; } = [];
            public string? Entrada { get; private set; }

            public override void Preparar(ICollection<Parametro> parametros)
            {
                Parametros = parametros.ToList();
            }

            public override Task<ResultadoComando> EjecutarAsync(
                string entrada,
                CancellationToken token = default)
            {
                Entrada = entrada;
                return Task.FromResult(ResultadoComando.Exito());
            }
        }
    }
}
