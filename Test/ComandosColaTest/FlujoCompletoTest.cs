using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Stream;
using Dapper;

namespace ComandosColaTest
{
    [Collection("Database")]
    public class FlujoCompletoTest : BaseIntegracionTest
    {
        private readonly RegistroComandos<string, ResultadoComando> _registro;
        private readonly AlmacenColaComandos _almacen;
        private readonly ILogger _logger;
        private readonly ConfiguracionPrueba _configuracion;

        protected override string PrefijoTest => "flujo_completo_";

        public FlujoCompletoTest(DatabaseFixture fixture) : base(fixture)
        {
            _registro = new RegistroComandos<string, ResultadoComando>(ConnectionString);
            _almacen = new AlmacenColaComandos(ConnectionString);
            _logger = NullLogger.Instance;
            _configuracion = new ConfiguracionPrueba();
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            ComandoPrueba.ResetearContador();
        }

        private async Task<ResultadoComando> EjecutarComandoAsync(
            IFactoriaComandos<string, ResultadoComando> factoria,
            ComandoEnCola comandoEnCola)
        {
            var lineaComando = ParsearLineaComando(comandoEnCola);
            var comando = factoria.Crear(lineaComando, _configuracion, _logger);
            var stream = new StreamEnMemoria<string, ResultadoComando>(comandoEnCola.DatosDeComando ?? string.Empty);

            await comando.EjecutarAsync(stream);

            return stream.ObtenerResultado() ?? ResultadoComando.Fallo("El comando no produjo resultado");
        }

        private PER.Comandos.LineaComandos.LineaComando ParsearLineaComando(ComandoEnCola comandoEnCola)
        {
            var partes = new List<string>();

            var rutaPartes = comandoEnCola.RutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            partes.AddRange(rutaPartes);

            if (!string.IsNullOrWhiteSpace(comandoEnCola.Argumentos))
            {
                var argumentosPartes = comandoEnCola.Argumentos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                partes.AddRange(argumentosPartes);
            }

            return new PER.Comandos.LineaComandos.LineaComando(partes);
        }

        [Fact]
        public async Task FlujoCompleto_RegistrarEncolarProcesar_DebeEjecutarComando()
        {
            var ruta = PrefijoTest + "orden procesar";
            var metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Procesa una orden"
            };
            var comandoPrueba = new ComandoPrueba("Orden procesada exitosamente");
            var nodo = new Nodo<string, ResultadoComando>(comandoPrueba);

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--orderId=123",
                DatosDeComando = "{\"orderId\": 123, \"total\": 500}",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);
            Assert.True(comandoId > 0);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();
            Assert.Single(pendientes);

            var comandoAProcesar = pendientes.First();
            Assert.Equal(ruta, comandoAProcesar.RutaComando);

            var contadorAntes = ComandoPrueba.ContadorEjecuciones;
            var resultado = await EjecutarComandoAsync(factoria, comandoAProcesar);
            
            Assert.True(resultado.Exitoso);
            Assert.Equal(contadorAntes + 1, ComandoPrueba.ContadorEjecuciones);

            await _almacen.MarcarComoProcesadoAsync(comandoAProcesar.Id, resultado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("Completado", (string)comandoDb.estado);
            Assert.Equal("Orden procesada exitosamente", (string)comandoDb.salida);
            Assert.NotNull(comandoDb.fecha_ejecucion);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoConParametros_DebeUsarParametros()
        {
            var ruta = PrefijoTest + "calculadora sumar";
            var metadatos = new MetadatosComando
            {
                RutaComando = ruta,
                Descripcion = "Suma dos numeros",
                EsquemaParametros = "{\"parametros\": [{\"nombre\": \"a\"}, {\"nombre\": \"b\"}]}"
            };
            var comandoSuma = new ComandoSuma();
            var nodo = new Nodo<string, ResultadoComando>(comandoSuma);

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                Argumentos = "--a=15 --b=27",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();
            var comando = pendientes.First();

            Assert.Equal(ruta, comando.RutaComando);
            Assert.Contains("--a=15", comando.Argumentos);
            Assert.Contains("--b=27", comando.Argumentos);

            var resultado = await EjecutarComandoAsync(factoria, comando);

            Assert.True(resultado.Exitoso);
            Assert.Contains("42", resultado.Salida);

            await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("Completado", (string)comandoDb.estado);
            Assert.Contains("42", (string)comandoDb.salida);
        }

        [Fact]
        public async Task FlujoCompleto_MultipleComandosEnCola_DebeProcesarEnOrden()
        {
            var ruta = PrefijoTest + "batch item";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var ids = new List<long>();
            for (int i = 1; i <= 5; i++)
            {
                var comando = new ComandoEnCola
                {
                    RutaComando = ruta,
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.UtcNow.AddMilliseconds(i * 10),
                    Estado = "Pendiente",
                    Intentos = 0
                };
                ids.Add(await _almacen.EncolarAsync(comando));
            }

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(100))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            Assert.Equal(5, pendientes.Count);

            for (int i = 0; i < 5; i++)
            {
                Assert.Contains($"--index={i + 1}", pendientes[i].Argumentos);
            }

            var contadorAntes = ComandoPrueba.ContadorEjecuciones;

            foreach (var comando in pendientes)
            {
                var resultado = await EjecutarComandoAsync(factoria, comando);
                Assert.True(resultado.Exitoso);
                await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);
            }

            Assert.Equal(contadorAntes + 5, ComandoPrueba.ContadorEjecuciones);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var completados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM cola_comandos WHERE estado = 'Completado' AND ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(5, completados);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoFalla_DebeRegistrarError()
        {
            var ruta = PrefijoTest + "proceso fallido";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("", deberiaFallar: true));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();
            var comando = pendientes.First();

            var resultado = await EjecutarComandoAsync(factoria, comando);

            Assert.False(resultado.Exitoso);
            Assert.Contains("Error simulado", resultado.MensajeError);

            await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("Fallido", (string)comandoDb.estado);
            Assert.Contains("Error simulado", (string)comandoDb.mensaje_error);
            Assert.Equal(1, (int)comandoDb.intentos);
        }

        [Fact]
        public async Task FlujoCompleto_ReintentarComandoFallido_DebeIncrementarIntentos()
        {
            var ruta = PrefijoTest + "reintento test";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("", deberiaFallar: true));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            var pendientes1 = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            var resultado1 = await EjecutarComandoAsync(factoria, pendientes1.First());
            Assert.False(resultado1.Exitoso);

            await _almacen.MarcarComoProcesadoAsync(pendientes1.First().Id, resultado1);

            using (var connection = CrearConexion())
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    "UPDATE cola_comandos SET estado = 'Pendiente', fecha_leido = NULL WHERE id = @Id",
                    new { Id = comandoId });
            }

            var pendientes2 = (await _almacen.ObtenerComandosPendientesAsync(10))
                .Where(c => c.RutaComando == ruta)
                .ToList();

            var resultado2 = await EjecutarComandoAsync(factoria, pendientes2.First());
            Assert.False(resultado2.Exitoso);

            await _almacen.MarcarComoProcesadoAsync(pendientes2.First().Id, resultado2);

            using var conn = CrearConexion();
            await conn.OpenAsync();

            var intentos = await conn.ExecuteScalarAsync<int>(
                "SELECT intentos FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal(2, intentos);
        }

        [Fact]
        public async Task FlujoCompleto_DesactivarComando_NoDebeAparecerEnFactoria()
        {
            var rutaActivo = PrefijoTest + "activo comando";
            var rutaInactivo = PrefijoTest + "inactivo comando";

            var metadatos1 = new MetadatosComando { RutaComando = rutaActivo };
            var metadatos2 = new MetadatosComando { RutaComando = rutaInactivo };

            var nodo1 = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            var nodo2 = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo1);
            await _registro.RegistrarComandoAsync(metadatos2, nodo2);

            await _registro.EliminarRegistroComandoAsync(rutaInactivo);

            var comandosActivos = (await _registro.ObtenerComandosRegistradosAsync())
                .Where(c => c.RutaComando.StartsWith(PrefijoTest));

            Assert.Single(comandosActivos);
            Assert.Equal(rutaActivo, comandosActivos.First().RutaComando);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoConDatosJson_DebePreservarDatos()
        {
            var ruta = PrefijoTest + "json test";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var datosJson = @"{
                ""cliente"": {
                    ""id"": 12345,
                    ""nombre"": ""Juan Perez"",
                    ""email"": ""juan@example.com""
                },
                ""items"": [
                    {""producto"": ""Laptop"", ""cantidad"": 1, ""precio"": 999.99},
                    {""producto"": ""Mouse"", ""cantidad"": 2, ""precio"": 25.50}
                ],
                ""total"": 1050.99,
                ""fecha"": ""2024-01-15T10:30:00Z""
            }";

            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                DatosDeComando = datosJson,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var datos = await connection.QuerySingleAsync<string>(
                "SELECT datos_comando::text FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Contains("Juan Perez", datos);
            Assert.Contains("12345", datos);
            Assert.Contains("Laptop", datos);
            Assert.Contains("1050.99", datos);
        }
    }
}
