using ComandosColaTest.Helpers;
using Moq;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Resultados;

namespace ComandosColaTest
{
    public class ResultadosComandosTest
    {
        [Fact]
        public async Task ObtenerResultadoAsync_CompletadoConPayload_DebeDeserializarSalida()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            RegistroProcesadoresResultadoComando registroProcesadores = new RegistroProcesadoresResultadoComando();
            ProcesadorResultadoTexto procesador = new ProcesadorResultadoTexto();
            registroProcesadores.Registrar("resultado_texto", procesador);

            almacen
                .Setup(a => a.ObtenerResultadoPersistidoAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResultadoComandoPersistido
                {
                    ComandoId = 10,
                    Estado = "completado",
                    Duracion = TimeSpan.FromMilliseconds(20),
                    PayloadResultado = new PayloadResultadoComando
                    {
                        Tipo = procesador.Tipo,
                        Version = procesador.Version,
                        Formato = "text/plain",
                        Contenido = "salida recuperada"
                    }
                });

            AlmacenamientoPayloadResultadoComando almacenamientoPayload = new AlmacenamientoPayloadResultadoComando(
                new OpcionesResultadosComandos());
            ResultadosComandos resultados = new ResultadosComandos(almacen.Object, registroProcesadores, almacenamientoPayload);

            ResultadoComando? resultado = await resultados.ObtenerResultadoAsync(10);

            Assert.NotNull(resultado);
            Assert.True(resultado.Exitoso);
            Assert.Equal("salida recuperada", resultado.Salida);
            Assert.Equal(TimeSpan.FromMilliseconds(20), resultado.Duracion);
        }

        [Fact]
        public async Task ObtenerResultadoAsync_CompletadoConRutaPayload_DebeLeerArchivoYDeserializarSalida()
        {
            string rutaBase = Path.Combine(Path.GetTempPath(), $"linea_comando_resultados_{Guid.NewGuid():N}");

            try
            {
                Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
                RegistroProcesadoresResultadoComando registroProcesadores = new RegistroProcesadoresResultadoComando();
                ProcesadorResultadoTexto procesador = new ProcesadorResultadoTexto();
                registroProcesadores.Registrar("resultado_texto", procesador);

                string rutaRelativa = "texto/v1/10.11111111111111111111111111111111.payload";
                string rutaCompleta = Path.Combine(rutaBase, rutaRelativa.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(rutaCompleta)!);
                await File.WriteAllTextAsync(rutaCompleta, "salida desde archivo");

                almacen
                    .Setup(a => a.ObtenerResultadoPersistidoAsync(10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ResultadoComandoPersistido
                    {
                        ComandoId = 10,
                        Estado = "completado",
                        Duracion = TimeSpan.FromMilliseconds(20),
                        PayloadResultado = new PayloadResultadoComando
                        {
                            Tipo = procesador.Tipo,
                            Version = procesador.Version,
                            Formato = procesador.Formato,
                            RutaPayload = rutaRelativa
                        }
                    });

                AlmacenamientoPayloadResultadoComando almacenamientoPayload = new AlmacenamientoPayloadResultadoComando(
                    new OpcionesResultadosComandos { RutaBase = rutaBase });
                ResultadosComandos resultados = new ResultadosComandos(almacen.Object, registroProcesadores, almacenamientoPayload);

                ResultadoComando? resultado = await resultados.ObtenerResultadoAsync(10);

                Assert.NotNull(resultado);
                Assert.True(resultado.Exitoso);
                Assert.Equal("salida desde archivo", resultado.Salida);
            }
            finally
            {
                if (Directory.Exists(rutaBase))
                    Directory.Delete(rutaBase, true);
            }
        }

        [Fact]
        public async Task ObtenerResultadoAsync_Pendiente_DebeRetornarNull()
        {
            Mock<IAlmacenColaComandos> almacen = new Mock<IAlmacenColaComandos>();
            RegistroProcesadoresResultadoComando registroProcesadores = new RegistroProcesadoresResultadoComando();

            almacen
                .Setup(a => a.ObtenerResultadoPersistidoAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResultadoComandoPersistido
                {
                    ComandoId = 10,
                    Estado = "pendiente"
                });

            AlmacenamientoPayloadResultadoComando almacenamientoPayload = new AlmacenamientoPayloadResultadoComando(
                new OpcionesResultadosComandos());
            ResultadosComandos resultados = new ResultadosComandos(almacen.Object, registroProcesadores, almacenamientoPayload);

            ResultadoComando? resultado = await resultados.ObtenerResultadoAsync(10);

            Assert.Null(resultado);
        }
    }
}
