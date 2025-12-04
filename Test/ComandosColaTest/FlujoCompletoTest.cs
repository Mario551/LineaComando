using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Persistence.Procesadores;
using PER.Comandos.LineaComandos.Persistence.Registro;
using PER.Comandos.LineaComandos.Registro;
using Dapper;

namespace ComandosColaTest
{
    /// <summary>
    /// Pruebas de integración del flujo completo:
    /// 1. Registrar comandos
    /// 2. Encolar comandos
    /// 3. Procesar cola
    /// 4. Verificar resultados
    /// </summary>
    public class FlujoCompletoTest : BaseIntegracionTest
    {
        private readonly RegistroComandos<string, ResultadoComando> _registro;
        private readonly AlmacenColaComandos _almacen;
        private readonly ILogger _logger;
        private readonly ConfiguracionPrueba _configuracion;

        public FlujoCompletoTest() : base()
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

        [Fact]
        public async Task FlujoCompleto_RegistrarEncolarProcesar_DebeEjecutarComando()
        {
            // === PASO 1: Registrar comando ===
            var metadatos = new MetadatosComando
            {
                RutaComando = "orden procesar",
                Descripcion = "Procesa una orden"
            };
            var comandoPrueba = new ComandoPrueba("Orden procesada exitosamente");
            var nodo = new Nodo<string, ResultadoComando>(comandoPrueba);

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Construir factoría
            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            // === PASO 2: Encolar comando ===
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = "orden procesar",
                Argumentos = "--orderId=123",
                DatosDeComando = "{\"orderId\": 123, \"total\": 500}",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);
            Assert.True(comandoId > 0);

            // === PASO 3: Procesar cola (simular procesador) ===
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(10);
            Assert.Single(pendientes);

            var comandoAProcesar = pendientes.First();
            Assert.Equal("orden procesar", comandoAProcesar.RutaComando);

            // Simular ejecución del comando
            var resultado = ResultadoComando.Exito("Orden procesada exitosamente", TimeSpan.FromMilliseconds(100));
            await _almacen.MarcarComoProcesadoAsync(comandoAProcesar.Id, resultado);

            // === PASO 4: Verificar resultado ===
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
            // Registrar comando de suma
            var metadatos = new MetadatosComando
            {
                RutaComando = "calculadora sumar",
                Descripcion = "Suma dos números",
                EsquemaParametros = "{\"parametros\": [{\"nombre\": \"a\"}, {\"nombre\": \"b\"}]}"
            };
            var comandoSuma = new ComandoSuma();
            var nodo = new Nodo<string, ResultadoComando>(comandoSuma);

            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Encolar comando con parámetros
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = "calculadora sumar",
                Argumentos = "--a=15 --b=27",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            // Obtener y verificar
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(10);
            var comando = pendientes.First();

            Assert.Equal("calculadora sumar", comando.RutaComando);
            Assert.Contains("--a=15", comando.Argumentos);
            Assert.Contains("--b=27", comando.Argumentos);

            // Marcar como completado
            var resultado = ResultadoComando.Exito("Resultado: 42", TimeSpan.FromMilliseconds(10));
            await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);

            // Verificar
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
            // Registrar comando
            var metadatos = new MetadatosComando { RutaComando = "batch item" };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Encolar múltiples comandos
            var ids = new List<long>();
            for (int i = 1; i <= 5; i++)
            {
                var comando = new ComandoEnCola
                {
                    RutaComando = "batch item",
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.UtcNow.AddMilliseconds(i * 10),
                    Estado = "Pendiente",
                    Intentos = 0
                };
                ids.Add(await _almacen.EncolarAsync(comando));
            }

            // Obtener pendientes
            var pendientes = (await _almacen.ObtenerComandosPendientesAsync(10)).ToList();

            Assert.Equal(5, pendientes.Count);

            // Verificar orden (por fecha de creación)
            for (int i = 0; i < 5; i++)
            {
                Assert.Contains($"--index={i + 1}", pendientes[i].Argumentos);
            }

            // Marcar todos como procesados
            foreach (var comando in pendientes)
            {
                await _almacen.MarcarComoProcesadoAsync(
                    comando.Id,
                    ResultadoComando.Exito($"Item {comando.Id} procesado"));
            }

            // Verificar todos completados
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var completados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM cola_comandos WHERE estado = 'Completado'");

            Assert.Equal(5, completados);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoFalla_DebeRegistrarError()
        {
            // Registrar comando
            var metadatos = new MetadatosComando { RutaComando = "proceso fallido" };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("", deberiaFallar: true));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Encolar
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = "proceso fallido",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            // Obtener y procesar
            var pendientes = await _almacen.ObtenerComandosPendientesAsync(10);
            var comando = pendientes.First();

            // Simular fallo
            var resultado = ResultadoComando.Fallo("Error de conexión a base de datos", TimeSpan.FromMilliseconds(50));
            await _almacen.MarcarComoProcesadoAsync(comando.Id, resultado);

            // Verificar estado de error
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("Fallido", (string)comandoDb.estado);
            Assert.Contains("Error de conexión", (string)comandoDb.mensaje_error);
            Assert.Equal(1, (int)comandoDb.intentos);
        }

        [Fact]
        public async Task FlujoCompleto_ReintentarComandoFallido_DebeIncrementarIntentos()
        {
            // Registrar comando
            var metadatos = new MetadatosComando { RutaComando = "reintento test" };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Encolar
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = "reintento test",
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            // Primer intento - falla
            var pendientes1 = await _almacen.ObtenerComandosPendientesAsync(10);
            await _almacen.MarcarComoProcesadoAsync(
                pendientes1.First().Id,
                ResultadoComando.Fallo("Primer intento fallido"));

            // Resetear estado para reintento
            using (var connection = CrearConexion())
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    "UPDATE cola_comandos SET estado = 'Pendiente', fecha_leido = NULL WHERE id = @Id",
                    new { Id = comandoId });
            }

            // Segundo intento - falla
            var pendientes2 = await _almacen.ObtenerComandosPendientesAsync(10);
            await _almacen.MarcarComoProcesadoAsync(
                pendientes2.First().Id,
                ResultadoComando.Fallo("Segundo intento fallido"));

            // Verificar intentos
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
            // Registrar comandos
            var metadatos1 = new MetadatosComando { RutaComando = "activo comando" };
            var metadatos2 = new MetadatosComando { RutaComando = "inactivo comando" };

            var nodo1 = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            var nodo2 = new Nodo<string, ResultadoComando>(new ComandoPrueba());

            await _registro.RegistrarComandoAsync(metadatos1, nodo1);
            await _registro.RegistrarComandoAsync(metadatos2, nodo2);

            // Desactivar uno
            await _registro.EliminarRegistroComandoAsync("inactivo comando");

            // Obtener comandos activos
            var comandosActivos = await _registro.ObtenerComandosRegistradosAsync();

            Assert.Single(comandosActivos);
            Assert.Equal("activo comando", comandosActivos.First().RutaComando);
        }

        [Fact]
        public async Task FlujoCompleto_ComandoConDatosJson_DebePreservarDatos()
        {
            // Registrar comando
            var metadatos = new MetadatosComando { RutaComando = "json test" };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            // Datos JSON complejos
            var datosJson = @"{
                ""cliente"": {
                    ""id"": 12345,
                    ""nombre"": ""Juan Pérez"",
                    ""email"": ""juan@example.com""
                },
                ""items"": [
                    {""producto"": ""Laptop"", ""cantidad"": 1, ""precio"": 999.99},
                    {""producto"": ""Mouse"", ""cantidad"": 2, ""precio"": 25.50}
                ],
                ""total"": 1050.99,
                ""fecha"": ""2024-01-15T10:30:00Z""
            }";

            // Encolar con datos JSON
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = "json test",
                DatosDeComando = datosJson,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await _almacen.EncolarAsync(comandoEnCola);

            // Verificar que los datos se guardaron correctamente
            using var connection = CrearConexion();
            await connection.OpenAsync();

            var datos = await connection.QuerySingleAsync<string>(
                "SELECT datos_comando::text FROM cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Contains("Juan Pérez", datos);
            Assert.Contains("12345", datos);
            Assert.Contains("Laptop", datos);
            Assert.Contains("1050.99", datos);
        }
    }
}
