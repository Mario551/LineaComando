using PER.Comandos.LineaComandos.Cola.Resultados;

namespace ComandosColaTest
{
    public class AlmacenamientoPayloadResultadoComandoTest
    {
        [Fact]
        public async Task GuardarAsync_ContenidoMenorA256Kb_DebeQuedarInline()
        {
            AlmacenamientoPayloadResultadoComando almacenamiento = new AlmacenamientoPayloadResultadoComando(
                new OpcionesResultadosComandos());
            PayloadResultadoComando payload = CrearPayload("contenido pequeno");

            PayloadResultadoComando? resultado = await almacenamiento.GuardarAsync(10, payload);

            Assert.NotNull(resultado);
            Assert.Equal("contenido pequeno", resultado.Contenido);
            Assert.Null(resultado.RutaPayload);
        }

        [Fact]
        public async Task GuardarAsync_ContenidoMayorA256Kb_DebeGuardarArchivo()
        {
            string rutaBase = Path.Combine(Path.GetTempPath(), $"linea_comando_payload_{Guid.NewGuid():N}");

            try
            {
                AlmacenamientoPayloadResultadoComando almacenamiento = new AlmacenamientoPayloadResultadoComando(
                    new OpcionesResultadosComandos { RutaBase = rutaBase });
                string contenido = new string('a', OpcionesResultadosComandos.TamanoMaximoPayloadBytes + 1);
                PayloadResultadoComando payload = CrearPayload(contenido);

                PayloadResultadoComando? resultado = await almacenamiento.GuardarAsync(10, payload);

                Assert.NotNull(resultado);
                Assert.Null(resultado.Contenido);
                Assert.NotNull(resultado.RutaPayload);
                Assert.Matches(@"^texto/v1/10\.[0-9a-f]{32}\.payload$", resultado.RutaPayload);

                string rutaCompleta = Path.Combine(rutaBase, resultado.RutaPayload.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(rutaCompleta));
                Assert.Equal(contenido, await File.ReadAllTextAsync(rutaCompleta));
            }
            finally
            {
                if (Directory.Exists(rutaBase))
                    Directory.Delete(rutaBase, true);
            }
        }

        [Fact]
        public async Task GuardarAsync_ContenidoMayorA256KbSinRutaBase_DebeFallar()
        {
            AlmacenamientoPayloadResultadoComando almacenamiento = new AlmacenamientoPayloadResultadoComando(
                new OpcionesResultadosComandos());
            string contenido = new string('a', OpcionesResultadosComandos.TamanoMaximoPayloadBytes + 1);
            PayloadResultadoComando payload = CrearPayload(contenido);

            await Assert.ThrowsAsync<InvalidOperationException>(() => almacenamiento.GuardarAsync(10, payload));
        }

        private static PayloadResultadoComando CrearPayload(string contenido)
        {
            return new PayloadResultadoComando
            {
                Tipo = "texto",
                Version = 1,
                Formato = "text/plain",
                Contenido = contenido
            };
        }
    }
}
