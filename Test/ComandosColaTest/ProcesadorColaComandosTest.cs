using Microsoft.Extensions.DependencyInjection;
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
        private readonly ILogger<ProcesadorColaComandos> _logger;

        protected override string PrefijoTest => "procesador_cola_";

        public ProcesadorColaComandosTest(DatabaseFixture fixture) : base(fixture)
        {
            _registro = new RegistroComandos<string, ResultadoComando>(ConnectionString);
            _logger = NullLogger<ProcesadorColaComandos>.Instance;
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            ComandoPrueba.ResetearContador();
        }

        private IServiceScopeFactory CrearServiceScopeFactory(FactoriaComandos<string, ResultadoComando> factoria)
        {
            var services = new ServiceCollection();
            services.AddScoped<IAlmacenColaComandos>(sp => new AlmacenColaComandos(ConnectionString));
            services.AddSingleton<IFactoriaComandos<string, ResultadoComando>>(factoria);

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IServiceScopeFactory>();
        }

        [Fact]
        public void Constructor_MaxParalelismoMenorOIgualACero_DebeLanzarExcepcion()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();
            var scopeFactory = CrearServiceScopeFactory(factoria);

            var excepcion = Assert.Throws<ArgumentException>(() =>
                new ProcesadorColaComandos(
                    scopeFactory,
                    0,
                    TimeSpan.FromSeconds(1),
                    _logger));

            Assert.Contains("máximo paralelismo", excepcion.Message.ToLower());
        }

        [Fact]
        public void Constructor_TiempoRefrescoMenorOIgualACero_DebeLanzarExcepcion()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();
            var scopeFactory = CrearServiceScopeFactory(factoria);

            var excepcion = Assert.Throws<ArgumentException>(() =>
                new ProcesadorColaComandos(
                    scopeFactory,
                    1,
                    TimeSpan.Zero,
                    _logger));

            Assert.Contains("tiempo de refresco", excepcion.Message.ToLower());
        }

        [Fact]
        public void Constructor_ScopeFactoryNulo_DebeLanzarExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorColaComandos(
                    null!,
                    1,
                    TimeSpan.FromSeconds(1),
                    _logger));
        }

        [Fact]
        public async Task StartAsync_ConCancelacion_DebeTerminarCorrectamente()
        {
            var factoria = new FactoriaComandos<string, ResultadoComando>();
            var scopeFactory = CrearServiceScopeFactory(factoria);
            var procesador = new ProcesadorColaComandos(
                scopeFactory,
                1,
                TimeSpan.FromMilliseconds(50),
                _logger);

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

            var almacen = new AlmacenColaComandos(ConnectionString);
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await almacen.EncolarAsync(comandoEnCola);

            var contadorAntes = ComandoPrueba.ContadorEjecuciones;

            var scopeFactory = CrearServiceScopeFactory(factoria);
            var procesador = new ProcesadorColaComandos(
                scopeFactory,
                1,
                TimeSpan.FromMilliseconds(50),
                _logger);

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

            var almacen = new AlmacenColaComandos(ConnectionString);
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var comandoId = await almacen.EncolarAsync(comandoEnCola);

            var scopeFactory = CrearServiceScopeFactory(factoria);
            var procesador = new ProcesadorColaComandos(
                scopeFactory,
                1,
                TimeSpan.FromMilliseconds(50),
                _logger);

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

            var almacen = new AlmacenColaComandos(ConnectionString);
            int cantidadComandos = 5;
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
                await almacen.EncolarAsync(comandoEnCola);
            }

            var contadorAntes = ComandoPrueba.ContadorEjecuciones;

            var scopeFactory = CrearServiceScopeFactory(factoria);
            var procesador = new ProcesadorColaComandos(
                scopeFactory,
                2,
                TimeSpan.FromMilliseconds(50),
                _logger);

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

            var almacen = new AlmacenColaComandos(ConnectionString);
            var comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.UtcNow,
                Estado = "Pendiente",
                Intentos = 0
            };

            var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
                () => almacen.EncolarAsync(comandoEnCola));

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

            var almacen = new AlmacenColaComandos(ConnectionString);
            int cantidadComandos = 4;
            for (int i = 0; i < cantidadComandos; i++)
            {
                var comandoEnCola = new ComandoEnCola
                {
                    RutaComando = ruta,
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "Pendiente",
                    Intentos = 0
                };
                await almacen.EncolarAsync(comandoEnCola);
            }

            var scopeFactory = CrearServiceScopeFactory(factoria);
            var procesador = new ProcesadorColaComandos(
                scopeFactory,
                2,
                TimeSpan.FromMilliseconds(50),
                _logger);

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
