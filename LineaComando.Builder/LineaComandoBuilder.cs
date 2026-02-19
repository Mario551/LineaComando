using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderInicializador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.EventDriven.Outbox;
using PER.Comandos.LineaComandos.EventDriven.Registro;
using PER.Comandos.LineaComandos.EventDriven.Servicio;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;
using PER.Comandos.LineaComandos.Stream;

namespace PER.Comandos.LineaComandos.Builder
{
    public class LineaComandoBuilder
    {
        private readonly IServiceCollection _services;
        private string? _connectionString;

        internal TimeSpan TiempoRefrescoCola { get; private set; } = TimeSpan.FromSeconds(1);
        internal TimeSpan TiempoRefrescoEventos { get; private set; } = TimeSpan.FromSeconds(1);
        internal TimeSpan TiempoRefrescoTareas { get; private set; } = TimeSpan.FromSeconds(1);
        internal int MaxParalelismoCola { get; private set; } = 4;

        internal Func<IServiceProvider, IBuilderInicializador, CancellationToken, Task> ConfiguracionLineaComandos;

        internal string ConnectionString => _connectionString ?? "";
        internal IServiceCollection Services => _services;

        public LineaComandoBuilder(IServiceCollection services, Func<IServiceProvider, IBuilderInicializador, CancellationToken, Task> configuracionLineaComandos)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            ConfiguracionLineaComandos = configuracionLineaComandos ?? throw new ArgumentNullException(nameof(configuracionLineaComandos));
        }

        public LineaComandoBuilder SetPostgresql(string connectionString)
        {
            _connectionString = connectionString;
            return this;
        }

        public LineaComandoBuilder SetMaxParalelismoCola(int max)
        {
            MaxParalelismoCola = max;
            return this;
        }

        public LineaComandoBuilder SetTiempoRefrescoColaComandos(TimeSpan tiempoRefresco)
        {
            TiempoRefrescoCola = tiempoRefresco;
            return this;
        }

        public LineaComandoBuilder SetTiempoRefrescoColaEventos(TimeSpan tiempoRefresco)
        {
            TiempoRefrescoEventos = tiempoRefresco;
            return this;
        }

        public LineaComandoBuilder SetTiempoRefrescoColaTareas(TimeSpan tiempoRefresco)
        {
            TiempoRefrescoTareas = tiempoRefresco;
            return this;
        }

        public void Build()
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new ArgumentException("Cadena de conexión debe estar definida");

            _services.AddSingleton(this);
            _services.AddTransient<IAlmacenColaComandos>(sp => new AlmacenColaComandos(_connectionString));
            _services.AddTransient<IRegistroManejadores>(sp => new RegistroManejadores(_connectionString));
            _services.AddSingleton<CoordinadorTareasProgramadas>();
            _services.AddHostedService<ServicioTareasProgramadas>();
            _services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(sp => new RegistroComandos<string, ResultadoComando>(_connectionString));
            _services.AddSingleton<IFactoriaComandos<string, ResultadoComando>, FactoriaComandos<string, ResultadoComando>>(c =>
            {
                var registro = c.GetRequiredService<IRegistroComandos<string, ResultadoComando>>();
                var factoria = new FactoriaComandos<string, ResultadoComando>();
                registro.ConstruirFactoriaAsync(factoria).GetAwaiter();

                return factoria;
            });

            _services.AddSingleton(sp =>
                new ProcesadorColaComandos(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    MaxParalelismoCola,
                    TiempoRefrescoCola,
                    sp.GetRequiredService<ILogger<ProcesadorColaComandos>>()));

            _services.AddHostedService<ServicioColaComandos>();
            _services.AddTransient<IColaEventos>(sp => new ColaEventos(_connectionString));
            _services.AddTransient<IRegistrarEvento>(sp => new RegistrarEvento(sp));
            _services.AddSingleton<IRegistroTiposEvento>(sp => new RegistroTiposEvento(_connectionString));
            _services.AddScoped<ProcesadorEventos>();
            _services.AddHostedService<ServicioProcesadorEventos>();
        }
    }
}
