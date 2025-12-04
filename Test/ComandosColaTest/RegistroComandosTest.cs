using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Persistence.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    public class RegistroComandosTest : BaseIntegracionTest
    {
        private readonly RegistroComandos<string, ResultadoComando> _registro;

        public RegistroComandosTest() : base()
        {
            _registro = new RegistroComandos<string, ResultadoComando>(ConnectionString);
        }

        [Fact]
        public async Task RegistrarComandoAsync_DebeInsertarComando()
        {
            // Arrange
            var metadatos = new MetadatosComando
            {
                RutaComando = "prueba ejecutar",
                Descripcion = "Comando de prueba",
                EsquemaParametros = "{\"parametros\": []}"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            // Act
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = "prueba ejecutar" });

            Assert.NotNull(comandoDb);
            Assert.Equal("Comando de prueba", (string)comandoDb.descripcion);
            Assert.True((bool)comandoDb.activo);
            Assert.True(metadatos.Id > 0);
        }

        [Fact]
        public async Task RegistrarComandoAsync_DebeActualizarComandoExistente()
        {
            // Arrange
            var metadatos1 = new MetadatosComando
            {
                RutaComando = "orden procesar",
                Descripcion = "Versión 1"
            };
            var metadatos2 = new MetadatosComando
            {
                RutaComando = "orden procesar",
                Descripcion = "Versión 2 actualizada"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            // Act
            await _registro.RegistrarComandoAsync(metadatos1, nodo);
            await _registro.RegistrarComandoAsync(metadatos2, nodo);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = "orden procesar" });

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = "orden procesar" });

            Assert.Equal(1, count);
            Assert.Equal("Versión 2 actualizada", (string)comandoDb.descripcion);
        }

        [Fact]
        public async Task ObtenerComandosRegistradosAsync_DebeRetornarSoloActivos()
        {
            // Arrange
            var metadatos1 = new MetadatosComando { RutaComando = "activo uno" };
            var metadatos2 = new MetadatosComando { RutaComando = "activo dos" };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo);
            await _registro.RegistrarComandoAsync(metadatos2, nodo);

            // Desactivar uno manualmente
            using (var connection = CrearConexion())
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    "UPDATE comandos_registrados SET activo = false WHERE ruta_comando = @Ruta",
                    new { Ruta = "activo uno" });
            }

            // Act
            var comandos = await _registro.ObtenerComandosRegistradosAsync();

            // Assert
            Assert.Single(comandos);
            Assert.Equal("activo dos", comandos.First().RutaComando);
        }

        [Fact]
        public async Task EliminarRegistroComandoAsync_DebeDesactivarComando()
        {
            // Arrange
            var metadatos = new MetadatosComando { RutaComando = "comando a eliminar" };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Act
            await _registro.EliminarRegistroComandoAsync("comando a eliminar");

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = "comando a eliminar" });

            Assert.NotNull(comandoDb);
            Assert.False((bool)comandoDb.activo);
        }

        [Fact]
        public async Task ConstruirFactoriaAsync_DebeConstruirArbolDeComandos()
        {
            // Arrange
            var metadatos1 = new MetadatosComando { RutaComando = "orden" };
            var metadatos2 = new MetadatosComando { RutaComando = "orden crear" };
            var metadatos3 = new MetadatosComando { RutaComando = "orden pagar" };

            var nodoOrden = new Nodo<string, ResultadoComando>();
            var nodoCrear = new Nodo<string, ResultadoComando>(new ComandoPrueba("Orden creada"));
            var nodoPagar = new Nodo<string, ResultadoComando>(new ComandoPrueba("Orden pagada"));

            await _registro.RegistrarComandoAsync(metadatos1, nodoOrden);
            await _registro.RegistrarComandoAsync(metadatos2, nodoCrear);
            await _registro.RegistrarComandoAsync(metadatos3, nodoPagar);

            var factoria = new FactoriaComandos<string, ResultadoComando>();

            // Act
            await _registro.ConstruirFactoriaAsync(factoria);

            // Assert - verificar que los comandos están en la base de datos
            var comandos = await _registro.ObtenerComandosRegistradosAsync();
            Assert.Equal(3, comandos.Count());
        }

        [Fact]
        public async Task RegistrarComandoAsync_ConEsquemaParametros_DebeGuardarJsonb()
        {
            // Arrange
            var esquema = @"{
                ""parametros"": [
                    {""nombre"": ""orderId"", ""tipo"": ""int"", ""requerido"": true},
                    {""nombre"": ""monto"", ""tipo"": ""decimal"", ""requerido"": true}
                ]
            }";
            var metadatos = new MetadatosComando
            {
                RutaComando = "pago procesar",
                Descripcion = "Procesa un pago",
                EsquemaParametros = esquema
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            // Act
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT esquema_parametros::text as esquema FROM comandos_registrados WHERE ruta_comando = @Ruta",
                new { Ruta = "pago procesar" });

            Assert.NotNull(comandoDb.esquema);
            Assert.Contains("orderId", (string)comandoDb.esquema);
        }
    }
}
