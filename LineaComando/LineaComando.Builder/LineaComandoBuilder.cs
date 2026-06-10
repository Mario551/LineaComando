using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.BaseDatos;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderInicializador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.EventDriven.Colas;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Outbox;
using PER.Comandos.LineaComandos.EventDriven.Registro;
using PER.Comandos.LineaComandos.EventDriven.Servicio;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;

namespace PER.Comandos.LineaComandos.Builder
{
    public class LineaComandoBuilder
    {
        private readonly IServiceCollection _services;
        private string? _connectionString;
        private string? _esquemaBaseDatos;
        private string? _rutaResultadosComandos;

        internal int MaxParalelismoCola { get; private set; } = 4;

        internal Func<IServiceProvider, IBuilderInicializador, CancellationToken, Task> ConfiguracionLineaComandos;

        public string ConnectionString => _connectionString ?? "";
        public string EsquemaBaseDatos => _esquemaBaseDatos ?? EsquemaPredeterminado();
        internal string? RutaResultadosComandos => _rutaResultadosComandos;
        public IServiceCollection Services => _services;
        public List<Func<IServiceProvider, LineaComandoBuilder, CancellationToken, Task>> InicializadoresExternos { get; } = new();

        public const int NONE = 0;
        public const int POSTGRESQL = 1;
        public const int SQLSERVER = 2;
        public const int SQLITE = 3;

        public int TipoBaseDatos { get; private set; }

        public LineaComandoBuilder(IServiceCollection services, Func<IServiceProvider, IBuilderInicializador, CancellationToken, Task> configuracionLineaComandos)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            ConfiguracionLineaComandos = configuracionLineaComandos ?? throw new ArgumentNullException(nameof(configuracionLineaComandos));
            TipoBaseDatos = NONE;
        }

        public LineaComandoBuilder UsePostgresql(string connectionString)
        {
            TipoBaseDatos = POSTGRESQL;
            _connectionString = connectionString;
            _esquemaBaseDatos ??= "public";
            return this;
        }

        public LineaComandoBuilder UsePostgresql(string connectionString, string esquema)
        {
            return UsePostgresql(connectionString)
                .SetEsquemaBaseDatos(esquema);
        }

        public LineaComandoBuilder UseSqlServer(string connectionString)
        {
            TipoBaseDatos = SQLSERVER;
            _connectionString = connectionString;
            _esquemaBaseDatos ??= "dbo";
            return this;
        }

        public LineaComandoBuilder UseSqlServer(string connectionString, string esquema)
        {
            return UseSqlServer(connectionString)
                .SetEsquemaBaseDatos(esquema);
        }

        public LineaComandoBuilder UseSqlite(string connectionString)
        {
            TipoBaseDatos = SQLITE;
            _connectionString = connectionString;
            return this;
        }

        public LineaComandoBuilder SetEsquemaBaseDatos(string esquema)
        {
            _esquemaBaseDatos = NombresBaseDatos.NormalizarEsquema(esquema, EsquemaPredeterminado());
            return this;
        }

        public LineaComandoBuilder SetRutaResultadosComandos(string rutaBase)
        {
            if (string.IsNullOrWhiteSpace(rutaBase))
                throw new ArgumentException("La ruta base de resultados de comandos no puede estar vacía.", nameof(rutaBase));

            _rutaResultadosComandos = rutaBase;
            return this;
        }

        public LineaComandoBuilder AgregarInicializadorExterno(Func<IServiceProvider, LineaComandoBuilder, CancellationToken, Task> inicializador)
        {
            InicializadoresExternos.Add(inicializador ?? throw new ArgumentNullException(nameof(inicializador)));
            return this;
        }

        public LineaComandoBuilder SetMaxParalelismoCola(int max)
        {
            MaxParalelismoCola = max;
            return this;
        }

        public void Build()
        {
            if (TipoBaseDatos == NONE)
                throw new ArgumentException("Debe elegir una base de datos");

            if (string.IsNullOrEmpty(_connectionString))
                throw new ArgumentException("Cadena de conexión debe estar definida");

            _services.AddSingleton(this);

            if (TipoBaseDatos == POSTGRESQL)
            {
                _services.AddTransient<IAlmacenColaComandos>(sp => new AlmacenColaComandosPostgres(_connectionString, EsquemaBaseDatos));
                _services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(sp => new RegistroComandosPostgres<string, ResultadoComando>(_connectionString, EsquemaBaseDatos));
                _services.AddTransient<IRegistroManejadores>(sp => new RegistroManejadoresPostgres(_connectionString, EsquemaBaseDatos));
                _services.AddTransient<IColaEventos>(sp => new ColaEventosPostgres(_connectionString, EsquemaBaseDatos));
                _services.AddSingleton<IRegistroTiposEvento>(sp => new RegistroTiposEventoPostgres(_connectionString, EsquemaBaseDatos));
            }
            else if (TipoBaseDatos == SQLSERVER)
            {
                _services.AddTransient<IAlmacenColaComandos>(sp => new AlmacenColaComandosSqlServer(_connectionString, EsquemaBaseDatos));
                _services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(sp => new RegistroComandosSqlServer<string, ResultadoComando>(_connectionString, EsquemaBaseDatos));
                _services.AddTransient<IRegistroManejadores>(sp => new RegistroManejadoresSqlServer(_connectionString, EsquemaBaseDatos));
                _services.AddTransient<IColaEventos>(sp => new ColaEventosSqlServer(_connectionString, EsquemaBaseDatos));
                _services.AddSingleton<IRegistroTiposEvento>(sp => new RegistroTiposEventoSqlServer(_connectionString, EsquemaBaseDatos));
            }

              _services.AddTransient<IRegistrarEventoBuilder>(sp => new RegistrarEventoBuilder(sp));
              _services.AddSingleton<IColaComandosMemoria, ColaComandosMemoria>();
              _services.AddSingleton(new OpcionesResultadosComandos { RutaBase = RutaResultadosComandos });
              _services.AddSingleton<IAlmacenamientoPayloadResultadoComando, AlmacenamientoPayloadResultadoComando>();
              _services.AddSingleton<IRegistroProcesadoresResultadoComando, RegistroProcesadoresResultadoComando>();
              _services.AddTransient<IResultadosComandos, ResultadosComandos>();
              _services.AddSingleton<IColaEventosMemoria, ColaEventosMemoria>();
              _services.AddSingleton<CoordinadorTareasProgramadas>();
              _services.AddSingleton<IPlanificadorTareasProgramadas>(
                  sp => sp.GetRequiredService<CoordinadorTareasProgramadas>());
              _services.AddHostedService<ServicioTareasProgramadas>();
              _services.AddSingleton<FactoriaComandos<string, ResultadoComando>>();
              _services.AddSingleton<IFactoriaComandos<string, ResultadoComando>>(
                  sp => sp.GetRequiredService<FactoriaComandos<string, ResultadoComando>>());

              _services.AddSingleton(sp =>
                  new ProcesadorColaComandos(
                      sp.GetRequiredService<IServiceScopeFactory>(),
                      sp.GetRequiredService<IColaComandosMemoria>(),
                      MaxParalelismoCola,
                      sp.GetRequiredService<ILogger<ProcesadorColaComandos>>()));

            _services.AddHostedService<ServicioColaComandos>();
            _services.AddScoped<ProcesadorEventos>();
            _services.AddHostedService<ServicioProcesadorEventos>();
        }

        private string EsquemaPredeterminado()
        {
            return TipoBaseDatos == SQLSERVER ? "dbo" : "public";
        }
    }
}
