using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ComandosColaTest.Helpers;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Registro;

namespace ComandosColaTest
{
    [Collection("DatabaseSqlServer")]
    public class ProcesadorColaComandosSqlServerTest : BaseIntegracionSqlServerTest
    {
        private readonly RegistroComandosSqlServer<string, ResultadoComando> _registro;
        private readonly ILogger<ProcesadorColaComandos> _logger;

        protected override string PrefijoTest => "procesador_cola_sql_";

        public ProcesadorColaComandosSqlServerTest(DatabaseFixtureSqlServer fixture) : base(fixture)
        {
            _registro = new RegistroComandosSqlServer<string, ResultadoComando>(ConnectionString, Esquema);
            _logger = NullLogger<ProcesadorColaComandos>.Instance;
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            ComandoPrueba.ResetearContador();
        }

        private IServiceScopeFactory CrearServiceScopeFactory(FactoriaComandos<string, ResultadoComando> factoria)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddScoped<IAlmacenColaComandos>(sp => new AlmacenColaComandosSqlServer(ConnectionString, Esquema));
            services.AddSingleton<IFactoriaComandos<string, ResultadoComando>>(factoria);

            ServiceProvider provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IServiceScopeFactory>();
        }

        private static IColaComandosMemoria CrearColaComandosMemoria(IServiceScopeFactory serviceScopeFactory)
        {
            return new ColaComandosMemoria(
                serviceScopeFactory,
                NullLogger<ColaComandosMemoria>.Instance);
        }

        [Fact]
        public void Constructor_MaxParalelismoMenorOIgualACero_DebeLanzarExcepcion()
        {
            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);

            ArgumentException excepcion = Assert.Throws<ArgumentException>(() =>
                new ProcesadorColaComandos(
                    scopeFactory,
                    CrearColaComandosMemoria(scopeFactory),
                    0,
                    _logger));

            Assert.Contains("máximo paralelismo", excepcion.Message.ToLower());
        }

        [Fact]
        public void Constructor_ColaComandosMemoriaNula_DebeLanzarExcepcion()
        {
            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);

            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorColaComandos(
                    scopeFactory,
                    null!,
                    1,
                    _logger));
        }

        [Fact]
        public void Constructor_ScopeFactoryNulo_DebeLanzarExcepcion()
        {
            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);

            Assert.Throws<ArgumentNullException>(() =>
                new ProcesadorColaComandos(
                    null!,
                    CrearColaComandosMemoria(scopeFactory),
                    1,
                    _logger));
        }

        [Fact]
        public async Task StartAsync_ConCancelacion_DebeTerminarCorrectamente()
        {
            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);
            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                scopeFactory,
                CrearColaComandosMemoria(scopeFactory),
                1,
                _logger);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            await procesador.StartAsync(cts.Token);
        }

        [Fact]
        public async Task StartAsync_ConComandoPendiente_DebeEjecutarComando()
        {
            string ruta = PrefijoTest + "comando simple";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("Ejecutado por procesador"));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            AlmacenColaComandosSqlServer almacen = new AlmacenColaComandosSqlServer(ConnectionString, Esquema);
            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await almacen.EncolarAsync(comandoEnCola);

            int contadorAntes = ComandoPrueba.ContadorEjecuciones;

            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);
            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                scopeFactory,
                CrearColaComandosMemoria(scopeFactory),
                1,
                _logger);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            await procesador.StartAsync(cts.Token);

            Assert.Equal(contadorAntes + 1, ComandoPrueba.ContadorEjecuciones);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            dynamic comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("completado", (string)comandoDb.estado);
        }

        [Fact]
        public async Task StartAsync_ConComandoQueFalla_DebeMarcarComoFallido()
        {
            string ruta = PrefijoTest + "comando fallido";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("", deberiaFallar: true));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            AlmacenColaComandosSqlServer almacen = new AlmacenColaComandosSqlServer(ConnectionString, Esquema);
            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            long comandoId = await almacen.EncolarAsync(comandoEnCola);

            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);
            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                scopeFactory,
                CrearColaComandosMemoria(scopeFactory),
                1,
                _logger);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            await procesador.StartAsync(cts.Token);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            dynamic comandoDb = await connection.QuerySingleAsync<dynamic>(
                $"SELECT * FROM {Nombres.ColaComandos} WHERE id = @Id",
                new { Id = comandoId });

            Assert.Equal("fallido", (string)comandoDb.estado);
            Assert.Contains("Error simulado", (string)comandoDb.mensaje_error);
        }

        [Fact]
        public async Task StartAsync_ConMultiplesComandos_DebeProcesarTodos()
        {
            string ruta = PrefijoTest + "multiples comandos";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba());
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            AlmacenColaComandosSqlServer almacen = new AlmacenColaComandosSqlServer(ConnectionString, Esquema);
            int cantidadComandos = 5;
            for (int i = 0; i < cantidadComandos; i++)
            {
                ComandoEnCola comandoEnCola = new ComandoEnCola
                {
                    RutaComando = ruta,
                    Argumentos = $"--index={i}",
                    FechaCreacion = DateTime.Now.AddMilliseconds(i * 10),
                    Estado = "pendiente",
                    Intentos = 0
                };
                await almacen.EncolarAsync(comandoEnCola);
            }

            int contadorAntes = ComandoPrueba.ContadorEjecuciones;

            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);
            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                scopeFactory,
                CrearColaComandosMemoria(scopeFactory),
                2,
                _logger);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            await procesador.StartAsync(cts.Token);

            Assert.Equal(contadorAntes + cantidadComandos, ComandoPrueba.ContadorEjecuciones);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            int completados = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {Nombres.ColaComandos} WHERE estado = 'completado' AND ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(cantidadComandos, completados);
        }

        [Fact]
        public async Task EncolarAsync_ConComandoNoRegistrado_DebeLanzarExcepcion()
        {
            string ruta = PrefijoTest + "comando no registrado";

            AlmacenColaComandosSqlServer almacen = new AlmacenColaComandosSqlServer(ConnectionString, Esquema);
            ComandoEnCola comandoEnCola = new ComandoEnCola
            {
                RutaComando = ruta,
                FechaCreacion = DateTime.Now,
                Estado = "pendiente",
                Intentos = 0
            };

            InvalidOperationException excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
                () => almacen.EncolarAsync(comandoEnCola));

            Assert.Contains("no está registrado", excepcion.Message);
        }

        [Fact]
        public async Task StartAsync_ConParalelismoLimitado_DebeRespetarLimite()
        {
            string ruta = PrefijoTest + "paralelismo test";
            MetadatosComando metadatos = new MetadatosComando { RutaComando = ruta };
            Nodo<string, ResultadoComando> nodo = new Nodo<string, ResultadoComando>(new ComandoPrueba("OK", tiempoEjecucionMs: 100));
            await _registro.RegistrarComandoAsync(metadatos, nodo);

            FactoriaComandos<string, ResultadoComando> factoria = new FactoriaComandos<string, ResultadoComando>();
            await _registro.ConstruirFactoriaAsync(factoria);

            AlmacenColaComandosSqlServer almacen = new AlmacenColaComandosSqlServer(ConnectionString, Esquema);
            int cantidadComandos = 4;
            for (int i = 0; i < cantidadComandos; i++)
            {
                ComandoEnCola comandoEnCola = new ComandoEnCola
                {
                    RutaComando = ruta,
                    FechaCreacion = DateTime.Now,
                    Estado = "pendiente",
                    Intentos = 0
                };
                await almacen.EncolarAsync(comandoEnCola);
            }

            IServiceScopeFactory scopeFactory = CrearServiceScopeFactory(factoria);
            ProcesadorColaComandos procesador = new ProcesadorColaComandos(
                scopeFactory,
                CrearColaComandosMemoria(scopeFactory),
                2,
                _logger);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await procesador.StartAsync(cts.Token);

            using Microsoft.Data.SqlClient.SqlConnection connection = CrearConexion();
            await connection.OpenAsync();

            int completados = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {Nombres.ColaComandos} WHERE estado = 'completado' AND ruta_comando = @Ruta",
                new { Ruta = ruta });

            Assert.Equal(cantidadComandos, completados);
        }
    }
}
