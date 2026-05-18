using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosColaTest
{
    public class ProcesadorColaComandosMemoriaTest
    {
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

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            factoria
                .Add("test")
                .Add("persistencia", new Nodo<string, ResultadoComando>(new ComandoPrueba("ok")));

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton<IFactoriaComandos<string, ResultadoComando>>(factoria)
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

            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                colaComandosMemoria.Object,
                1,
                NullLogger<ProcesadorColaComandos>.Instance);

            await procesador.StartAsync(CancellationToken.None);

            Assert.Equal(new[] { "persistido", "entregado" }, orden);
        }

        private static async IAsyncEnumerable<ComandoEnCola> LeerComandosAsync(IEnumerable<ComandoEnCola> comandos)
        {
            foreach (ComandoEnCola comando in comandos)
            {
                await Task.Yield();
                yield return comando;
            }
        }
    }
}
