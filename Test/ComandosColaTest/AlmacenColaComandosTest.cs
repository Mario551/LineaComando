using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Persistence.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    public class AlmacenColaComandosTest : BaseIntegracionTest
    {
        private readonly AlmacenColaComandos _almacen;
        private readonly RegistroComandos<string, ResultadoComando> _registro;

        public AlmacenColaComandosTest() : base()
        {
            _almacen = new AlmacenColaComandos(ConnectionString);
            _registro = new RegistroComandos<string, ResultadoComando>(ConnectionString);
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            // Registrar un comando base para las pruebas
            await RegistrarComandoBaseAsync();
        }

        private async Task RegistrarComandoBaseAsync()
        {
            var metadatos = new MetadatosComando
            {
                RutaComando = "test ejecutar",
                Descripcion = "Comando de prueba para tests"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);
        }

        [Fact]
        public async Task EncolarAsync_DebeInsertarComandoEnCola()
        {
            // Arrange
            var comando = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                Argumentos = "--mensaje=hola",
                DatosDeComando = "{\"key\": \"value\"}",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            // Act
            var id = await _almacen.EncolarAsync(comando);

            // Assert
            Assert.True(id > 0);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.NotNull(comandoDb);
            Assert.Equal("test ejecutar", (string)comandoDb.ruta_comando);
            Assert.Equal("--mensaje=hola", (string)comandoDb.argumentos);
            Assert.Equal("Pendiente", (string)comandoDb.estado);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeRetornarComandosPendientes()
        {
            // Arrange
            var comando1 = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                Argumentos = "--id=1",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };
            var comando2 = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                Argumentos = "--id=2",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            await _almacen.EncolarAsync(comando1);
            await _almacen.EncolarAsync(comando2);

            // Act
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(10);

            // Assert
            Assert.Equal(2, pendientes.Count());
            Assert.All(pendientes, c => Assert.Equal("Procesando", c.Estado));
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeMarcarComoProcesando()
        {
            // Arrange
            var comando = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                Argumentos = "--test=true",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            // Act
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(10);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.Equal("Procesando", (string)comandoDb.estado);
            Assert.NotNull(comandoDb.fecha_leido);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_NoDebeRetornarComandosYaLeidos()
        {
            // Arrange
            var comando = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            await _almacen.EncolarAsync(comando);

            // Primera lectura
            await _almacen.ObtenerComandosPendientesAsync(10);

            // Act - Segunda lectura
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(10);

            // Assert
            Assert.Empty(pendientes);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_DebeActualizarEstadoExitoso()
        {
            // Arrange
            var comando = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);
            await _almacen.ObtenerComandosPendientesAsync(10);

            var resultado = ResultadoComando.Exito("Procesado correctamente", TimeSpan.FromMilliseconds(150));

            // Act
            await _almacen.MarcarComoProcesadoAsync(id, resultado);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.Equal("Completado", (string)comandoDb.estado);
            Assert.NotNull(comandoDb.fecha_ejecucion);
            Assert.Equal("Procesado correctamente", (string)comandoDb.salida);
            Assert.Equal(150, (long)comandoDb.duracion_ms);
            Assert.Null(comandoDb.mensaje_error);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_DebeActualizarEstadoFallido()
        {
            // Arrange
            var comando = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);
            await _almacen.ObtenerComandosPendientesAsync(10);

            var resultado = ResultadoComando.Fallo("Error de conexión", TimeSpan.FromMilliseconds(50));

            // Act
            await _almacen.MarcarComoProcesadoAsync(id, resultado);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.Equal("Fallido", (string)comandoDb.estado);
            Assert.Equal("Error de conexión", (string)comandoDb.mensaje_error);
            Assert.Equal(1, (int)comandoDb.intentos);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeRespetarTamanioLote()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                var comando = new ComandoEnCola
                {
                    RutaComando = "test ejecutar",
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "Pendiente",
                    Intentos = 0
                };
                await _almacen.EncolarAsync(comando);
            }

            // Act
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(3);

            // Assert
            Assert.Equal(3, pendientes.Count());
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeOrdenarPorFechaCreacion()
        {
            // Arrange
            var fechaBase = DateTime.UtcNow;

            var comando1 = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                Argumentos = "--orden=segundo",
                FechaCreacion = fechaBase.AddSeconds(1),
                Estado = "Pendiente",
                Intentos = 0
            };
            var comando2 = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                Argumentos = "--orden=primero",
                FechaCreacion = fechaBase,
                Estado = "Pendiente",
                Intentos = 0
            };

            await _almacen.EncolarAsync(comando1);
            await _almacen.EncolarAsync(comando2);

            // Act
            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(10)).ToList();

            // Assert
            Assert.Equal(2, pendientes.Count);
            Assert.Contains("primero", pendientes[0].Argumentos);
            Assert.Contains("segundo", pendientes[1].Argumentos);
        }

        [Fact]
        public async Task EncolarAsync_ConDatosJsonb_DebeGuardarCorrectamente()
        {
            // Arrange
            var datosJson = @"{
                ""orderId"": 12345,
                ""items"": [{""sku"": ""ABC"", ""cantidad"": 2}],
                ""total"": 99.99
            }";

            var comando = new ComandoEnCola
            {
                RutaComando = "test ejecutar",
                DatosDeComando = datosJson,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            // Act
            var id = await _almacen.EncolarAsync(comando);

            // Assert
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var datosDb = await connection.QuerySingleAsync<string>(
                "SELECT datos_comando::text FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.Contains("12345", datosDb);
            Assert.Contains("ABC", datosDb);
        }
    }
}
