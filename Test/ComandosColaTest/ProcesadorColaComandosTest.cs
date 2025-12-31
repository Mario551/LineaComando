using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;
using Dapper;

namespace ComandosColaTest
{
    [Collection("Database")]
    public class ProcesadorColaComandosTest : BaseIntegracionTest
    {
        private readonly RegistroComandos<string, ResultadoComando> _registro;
        private readonly AlmacenColaComandos _almacen;
        private readonly ILogger _logger;
        private readonly ConfiguracionPrueba _configuracion;

        protected override string PrefijoTest => "procesador_cola_";

        public ProcesadorColaComandosTest(DatabaseFixture fixture) : base(fixture)
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
        public void Constructor_MaxParalelismoMenorOIgualACero_DebeLanzarExcepcion()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();

            var excepcion = Assert.Throws<ArgumentException>(() =>
                new ProcesadorColaComandos(
                    _almacen,
                    factoria,
                    _configuracion,
                    _logger,
                    maxParalelismo: 0,
                    tiempoRefresco: TimeSpan.FromSeconds(1)));

            Assert.Contains("máximo paralelismo", excepcion.Message.ToLower());
        }

        [Fact]
        public void Constructor_TiempoRefrescoMenorOIgualACero_DebeLanzarExcepcion()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();

            var excepcion = Assert.Throws<ArgumentException>(() =>
                new ProcesadorColaComandos(
                    _almacen,
                    factoria,
                    _configuracion,
                    _logger,
                    maxParalelismo: 1,
                    tiempoRefresco: TimeSpan.Zero));

            Assert.Contains("tiempo de refresco", excepcion.Message.ToLower());
        }

        [Fact]
        public void Constructor_AlmacenNulo_DebeLanzarExcepcion()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();

            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorColaComandos(
                    null!,
                    factoria,
                    _configuracion,
                    _logger,
                    maxParalelismo: 1,
                    tiempoRefresco: TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Constructor_FactoriaNula_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorColaComandos(
                    _almacen,
                    null!,
                    _configuracion,
                    _logger,
                    maxParalelismo: 1,
                    tiempoRefresco: TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task StartAsync_ConCancelacion_DebeTerminarCorrectamente()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();
            var procesador = new ProcesadorColaComandos(
                _almacen,
                factoria,
                _configuracion,
                _logger,
                maxParalelismo: 1,
                tiempoRefresco: TimeSpan.FromMilliseconds(50));

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            await procesador.StartAsync(cts.Token);
        }

        [Fact]
        public async Task StartAsync_ConComandoPendiente_DebeEjecutarComando()
        {
            var ruta = PrefijoTest + "comando simple";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("Ejecutado por procesador"));
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

            var contadorAntes = ComandoPrueba.ContadorEjecuciones;

            var procesador = new ProcesadorColaComandos(
                _almacen,
                factoria,
                _configuracion,
                _logger,
                maxParalelismo: 1,
                tiempoRefresco: TimeSpan.FromMilliseconds(50));

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            await procesador.StartAsync(cts.Token);

            Assert.Equal(contadorAntes + 1, ComandoPrueba.ContadorEjecuciones);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM per_cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("Completado", (string)comandoDb.estado);
            Assert.Equal("Ejecutado por procesador", (string)comandoDb.salida);
        }

        [Fact]
        public async Task StartAsync_ConComandoQueFalla_DebeMarcarComoFallido()
        {
            var ruta = PrefijoTest + "comando fallido";
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

            var procesador = new ProcesadorColaComandos(
                _almacen,
                factoria,
                _configuracion,
                _logger,
                maxParalelismo: 1,
                tiempoRefresco: TimeSpan.FromMilliseconds(50));

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            await procesador.StartAsync(cts.Token);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var comandoDb = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM per_cola_comandos WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("Fallido", (string)comandoDb.estado);
            Assert.Contains("Error simulado", (string)comandoDb.mensaje_error);
        }

        [Fact]
        public async Task StartAsync_ConMultiplesComandos_DebeProcesarTodos()
        {
            var ruta = PrefijoTest + "multiples comandos";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var cantidadComandos = 5;
            for (int i = 0; i < cantidadComandos; i++)
            {
                var comandoEnCola = new ComandoEnCola
                {
                    RutaComando = ruta,
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.UtcNow.AddMilliseconds(i * 10),
                    Estado = "Pendiente",
                    Intentos = 0
                };
                await _almacen.EncolarAsync(comandoEnCola);
            }

            var contadorAntes = ComandoPrueba.ContadorEjecuciones;

            var procesador = new ProcesadorColaComandos(
                _almacen,
                factoria,
                _configuracion,
                _logger,
                maxParalelismo: 2,
                tiempoRefresco: TimeSpan.FromMilliseconds(50));

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            await procesador.StartAsync(cts.Token);

            Assert.Equal(contadorAntes + cantidadComandos, ComandoPrueba.ContadorEjecuciones);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var completados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM per_cola_comandos WHERE estado = 'Completado' AND ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(cantidadComandos, completados);
        }

        [Fact]
        public async Task EncolarAsync_ConComandoNoRegistrado_DebeLanzarExcepcion()
        {
            var ruta = PrefijoTest + "comando no registrado";

            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _almacen.EncolarAsync(comandoEnCola));

            Assert.Contains("no está registrado", excepcion.Message);
        }

        [Fact]
        public async Task StartAsync_ConParalelismoLimitado_DebeRespetarLimite()
        {
            var ruta = PrefijoTest + "paralelismo test";
            var metadatos = new MetadatosComando { RutaComando = ruta };
            var nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("OK", tiempoEjecucionMs: 100));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            var factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            var cantidadComandos = 4;
            for (int i = 0; i < cantidadComandos; i++)
            {
                var comandoEnCola = new ComandoEnCola
                {
                    RutaComando = ruta,
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "Pendiente",
                    Intentos = 0
                };
                await _almacen.EncolarAsync(comandoEnCola);
            }

            var procesador = new ProcesadorColaComandos(
                _almacen,
                factoria,
                _configuracion,
                _logger,
                maxParalelismo: 2,
                tiempoRefresco: TimeSpan.FromMilliseconds(50));

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await procesador.StartAsync(cts.Token);

            using var connection = CrearConexion();
            await connection.OpenAsync();

            var completados = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM per_cola_comandos WHERE estado = 'Completado' AND ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(cantidadComandos, completados);
        }
    }
}
