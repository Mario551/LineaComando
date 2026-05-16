using Microsoft.Extensions.DependencyInjection;
using Moq;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Resultados;

namespace ComandosColaTest
{
    public class ColaComandosMemoriaTest
    {
        [Fact]
        public async Task EncolarAsync_DebePersistirEncolarYCompletarResultado()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.EncolarAsync(It.IsAny<ComandoEnCola>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            ComandoEncolado comando = await cola.EncolarAsync(new SolicitudComando
            {
                RutaComando = "test ejecutar",
                Argumentos = "--origen=test",
                DatosDeComando = "{}"
            });

            Assert.Equal(10, comando.ComandoId);
            Assert.False(comando.Resultado.IsCompleted);

            object salida = new { Mensaje = "ok", Codigo = 200 };
            cola.CompletarResultado(10, ResultadoComando.Exito(salida));

            ResultadoComando resultado = await comando.Resultado.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(resultado.Exitoso);
            Assert.Same(salida, resultado.Salida);
        }

        [Fact]
        public async Task CargarPendientesDesdeBaseDatosAsync_DebeEncolarPendientesEnMemoria()
        {
            ComandoEnCola pendiente = new ComandoEnCola
            {
                Id = 20,
                RutaComando = "test pendiente",
                Argumentos = string.Empty,
                DatosDeComando = "{}"
            };

            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.ObtenerComandosPendientesAsync(int.MaxValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { pendiente });

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            await cola.CargarPendientesDesdeBaseDatosAsync();

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using IAsyncEnumerator<ComandoEnCola> enumerador = cola.LeerAsync(cts.Token).GetAsyncEnumerator();

            Assert.True(await enumerador.MoveNextAsync());
            Assert.Equal(20, enumerador.Current.Id);
            Assert.Equal("test pendiente", enumerador.Current.RutaComando);
        }

        [Fact]
        public async Task CargarPendientesDesdeBaseDatosAsync_DebeCrearEsperaRecuperable()
        {
            ComandoEnCola pendiente = new ComandoEnCola
            {
                Id = 20,
                RutaComando = "test pendiente",
                Argumentos = string.Empty,
                DatosDeComando = "{}"
            };

            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.ObtenerComandosPendientesAsync(int.MaxValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { pendiente });

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton(Mock.Of<IResultadosComandos>())
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            await cola.CargarPendientesDesdeBaseDatosAsync();

            ComandoEncolado comando = await cola.EsperarComandoAsync(20);

            Assert.Equal(20, comando.ComandoId);
            Assert.False(comando.Resultado.IsCompleted);

            string salida = "resultado desde worker";
            cola.CompletarResultado(20, ResultadoComando.Exito(salida));

            ResultadoComando resultado = await comando.Resultado.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(resultado.Exitoso);
            Assert.Equal(salida, resultado.Salida);
        }

        [Fact]
        public async Task EsperarComandoAsync_DespuesDeLimpiarEspera_DebeRetornarResultadoDurable()
        {
            ComandoEnCola pendiente = new ComandoEnCola
            {
                Id = 30,
                RutaComando = "test pendiente",
                Argumentos = string.Empty,
                DatosDeComando = "{}"
            };

            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.ObtenerComandosPendientesAsync(int.MaxValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { pendiente });

            Mock<IResultadosComandos> resultados = new Mock<IResultadosComandos>();
            resultados
                .Setup(r => r.ObtenerResultadoAsync(30, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ResultadoComando.Exito("resultado durable"));

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton(resultados.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            await cola.CargarPendientesDesdeBaseDatosAsync();
            cola.CompletarResultado(30, ResultadoComando.Exito("resultado no reclamado"));

            ComandoEncolado comando = await cola.EsperarComandoAsync(30);
            ResultadoComando resultado = await comando.Resultado;

            Assert.True(resultado.Exitoso);
            Assert.Equal("resultado durable", resultado.Salida);
        }

        [Fact]
        public async Task EsperarComandoAsync_ComandoCompletado_DebeRetornarResultadoDurable()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            Mock<IResultadosComandos> resultados = new Mock<IResultadosComandos>();
            resultados
                .Setup(r => r.ObtenerResultadoAsync(40, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ResultadoComando.Exito("resultado durable"));

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton(resultados.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            ComandoEncolado comando = await cola.EsperarComandoAsync(40);
            ResultadoComando resultado = await comando.Resultado;

            Assert.Equal(40, comando.ComandoId);
            Assert.True(resultado.Exitoso);
            Assert.Equal("resultado durable", resultado.Salida);
        }

        [Fact]
        public async Task EsperarComandoAsync_ComandoFallido_DebeRetornarFalloDurable()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            Mock<IResultadosComandos> resultados = new Mock<IResultadosComandos>();
            resultados
                .Setup(r => r.ObtenerResultadoAsync(50, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ResultadoComando.Fallo("fallo durable"));

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton(resultados.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            ComandoEncolado comando = await cola.EsperarComandoAsync(50);
            ResultadoComando resultado = await comando.Resultado;

            Assert.False(resultado.Exitoso);
            Assert.Equal("fallo durable", resultado.MensajeError);
        }

        [Fact]
        public async Task EsperarComandoAsync_ComandoPendiente_DosConsumidoresDebenRecibirMismoResultado()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.ObtenerResultadoPersistidoAsync(60, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResultadoComandoPersistido
                {
                    ComandoId = 60,
                    Estado = "pendiente"
                });

            Mock<IResultadosComandos> resultados = new Mock<IResultadosComandos>();
            resultados
                .Setup(r => r.ObtenerResultadoAsync(60, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ResultadoComando?)null);

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton(resultados.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            ComandoEncolado primerComando = await cola.EsperarComandoAsync(60);
            ComandoEncolado segundoComando = await cola.EsperarComandoAsync(60);

            Assert.Same(primerComando.Resultado, segundoComando.Resultado);

            object salida = new { Mensaje = "ok" };
            cola.CompletarResultado(60, ResultadoComando.Exito(salida));

            ResultadoComando primerResultado = await primerComando.Resultado.WaitAsync(TimeSpan.FromSeconds(1));
            ResultadoComando segundoResultado = await segundoComando.Resultado.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Same(salida, primerResultado.Salida);
            Assert.Same(salida, segundoResultado.Salida);
        }

        [Fact]
        public async Task EsperarComandoAsync_ComandoInexistente_DebeLanzarExcepcion()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            almacen
                .Setup(a => a.ObtenerResultadoPersistidoAsync(70, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ResultadoComandoPersistido?)null);

            Mock<IResultadosComandos> resultados = new Mock<IResultadosComandos>();
            resultados
                .Setup(r => r.ObtenerResultadoAsync(70, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ResultadoComando?)null);

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton(almacen.Object)
                .AddSingleton(resultados.Object)
                .BuildServiceProvider();

            ColaComandosMemoria cola = new ColaComandosMemoria(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
                () => cola.EsperarComandoAsync(70));

            Assert.Contains("no existe", excepcion.Message);
        }
    }
}
