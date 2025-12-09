using Dapper;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    [Collection("Database")]
    public class AlmacenColaComandosTest : BaseIntegracionTest
    {
        private readonly AlmacenColaComandos _almacen;
        private readonly RegistroComandos<string, ResultadoComando> _registro;

        protected override string PrefijoTest => "almacen_cola_";

        public AlmacenColaComandosTest(DatabaseFixture fixture) : base(fixture)
        {
            _almacen = new AlmacenColaComandos(ConnectionString);
            _registro = new RegistroComandos<string, ResultadoComando>(ConnectionString);
        }

        private async Task PrepararTestAsync(string rutaComando)
        {
            var metadatos = new MetadatosComando
            {
                RutaComando = rutaComando,
                Descripcion = $"Comando de prueba: {rutaComando}"
            };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);
        }

        [Fact]
        public async Task EncolarAsync_DebeInsertarComandoEnCola()
        {
            var ruta = PrefijoTest + "encolar_insertar";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--mensaje=hola",
                DatosDeComando = "{\"key\": \"value\"}",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            Assert.True(id > 0);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.NotNull(comandoDb);
            Assert.Equal(ruta, (string)comandoDb.ruta_comando);
            Assert.Equal("--mensaje=hola", (string)comandoDb.argumentos);
            Assert.Equal("Pendiente", (string)comandoDb.estado);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeRetornarComandosPendientes()
        {
            var ruta = PrefijoTest + "obtener_pendientes";
            await PrepararTestAsync(ruta);

            var comando1 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--id=1",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };
            var comando2 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--id=2",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            long c1 = await _almacen.EncolarAsync(comando1);
            long c2 = await _almacen.EncolarAsync(comando2);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            Assert.Equal(2, pendientes.Count);
            Assert.Contains(c1, pendientes.Select(e => e.Id));
            Assert.Contains(c2, pendientes.Select(e => e.Id));
            Assert.All(pendientes, c => Assert.Equal("Pendiente", c.Estado));
        }

        [Fact]
        public async Task MarcarComandosProcesandoAsync_DebeMarcarComoProcesando()
        {
            var ruta = PrefijoTest + "marcar_procesando";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--test=true",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            var procesando = await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            Assert.Single(procesando);
            Assert.Equal("Procesando", procesando.First().Estado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = id });

            Assert.Equal("Procesando", (string)comandoDb.estado);
            Assert.NotNull(comandoDb.fecha_leido);
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_NoDebeRetornarComandosYaMarcados()
        {
            var ruta = PrefijoTest + "no_retornar_marcados";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta);

            await _almacen.MarcarComandosProcesandoAsync(pendientes.Select(c => c.Id).ToArray());

            var pendientesSegundaLectura = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta);

            Assert.Empty(pendientesSegundaLectura);
        }

        [Fact]
        public async Task MarcarComoProcesadoAsync_DebeActualizarEstadoExitoso()
        {
            var ruta = PrefijoTest + "estado_exitoso";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);
            await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            var resultado = ResultadoComando.Exito("Procesado correctamente", TimeSpan.FromMilliseconds(150));

            await _almacen.MarcarComoProcesadoAsync(id, resultado);

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
            var ruta = PrefijoTest + "estado_fallido";
            await PrepararTestAsync(ruta);

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);
            await _almacen.MarcarComandosProcesandoAsync(new[] { id });

            var resultado = ResultadoComando.Fallo("Error de conexión", TimeSpan.FromMilliseconds(50));

            await _almacen.MarcarComoProcesadoAsync(id, resultado);

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
            var ruta = PrefijoTest + "tamanio_lote";
            await PrepararTestAsync(ruta);

            for (int i = 0; i < 10; i++)
            {
                var comando = new ComandoEnCola
                {
                    RutaComando = ruta,
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "Pendiente",
                    Intentos = 0
                };
                await _almacen.EncolarAsync(comando);
            }

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .Take(3);

            Assert.Equal(3, pendientes.Count());
        }

        [Fact]
        public async Task ObtenerComandosPendientesAsync_DebeOrdenarPorFechaCreacion()
        {
            var ruta = PrefijoTest + "ordenar_fecha";
            await PrepararTestAsync(ruta);

            var fechaBase = DateTime.UtcNow;

            var comando1 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--orden=primero",
                FechaCreacion = fechaBase.AddSeconds(1),
                Estado = "Pendiente",
                Intentos = 0
            };
            var comando2 = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--orden=segundo",
                FechaCreacion = fechaBase,
                Estado = "Pendiente",
                Intentos = 0
            };

            System.Diagnostics.Debugger.Launch();

            await _almacen.EncolarAsync(comando1);
            await _almacen.EncolarAsync(comando2);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            Assert.Equal(2, pendientes.Count);
            Assert.Contains("--orden=primero", pendientes[0].Argumentos);
            Assert.Contains("--orden=segundo", pendientes[1].Argumentos);
        }

        [Fact]
        public async Task EncolarAsync_ConDatosJsonb_DebeGuardarCorrectamente()
        {
            var ruta = PrefijoTest + "encolar_jsonb";
            await PrepararTestAsync(ruta);

            var datosJson = @"{
                ""orderId"": 12345,
                ""items"": [{""sku"": ""ABC"", ""cantidad"": 2}],
                ""total"": 99.99
            }";

            var comando = new ComandoEnCola
            {
                RutaComando = ruta,
                DatosDeComando = datosJson,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var id = await _almacen.EncolarAsync(comando);

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
