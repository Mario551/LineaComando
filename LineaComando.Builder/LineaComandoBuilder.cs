using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Procesadores;
using PER.Comandos.LineaComandos.Cola.Registro;
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
        private readonly string _connectionString;

        internal bool UsarCola { get; private set; }
        internal bool UsarEventDriven { get; private set; }
        internal TimeSpan TiempoRefrescoCola { get; private set; } = TimeSpan.FromSeconds(1);
        internal TimeSpan TiempoRefrescoEventos { get; private set; } = TimeSpan.FromSeconds(1);
        internal TimeSpan TiempoRefrescoTareas { get; private set; } = TimeSpan.FromSeconds(1);
        internal int MaxParalelismoCola { get; private set; } = 4;

        internal Func<IServiceProvider, IRegistroComandos<string, ResultadoComando>, CancellationToken, Task>? ConfigurarComandos { get; private set; }
        internal Func<IServiceProvider, IRegistroTiposEvento, CancellationToken, Task>? ConfigurarEventos { get; private set; }

        internal string ConnectionString => _connectionString;
        internal IServiceCollection Services => _services;

        public LineaComandoBuilder(IServiceCollection services, string connectionString)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public LineaComandoBuilder ConColaComandos(
            Func<IServiceProvider, IRegistroComandos<string, ResultadoComando>, CancellationToken, Task> configurarComandos,
            TimeSpan? tiempoRefresco = null,
            int maxParalelismo = 4)
        {
            UsarCola = true;
            ConfigurarComandos = configurarComandos;
            if (tiempoRefresco.HasValue)
                TiempoRefrescoCola = tiempoRefresco.Value;
            MaxParalelismoCola = maxParalelismo;
            return this;
        }

        public LineaComandoBuilder ConEventDriven(
            Func<IServiceProvider, IRegistroTiposEvento, CancellationToken, Task> configurarEventos,
            TimeSpan? tiempoRefrescoEventos = null,
            TimeSpan? tiempoRefrescoTareas = null)
        {
            UsarEventDriven = true;
            ConfigurarEventos = configurarEventos;
            if (tiempoRefrescoEventos.HasValue)
                TiempoRefrescoEventos = tiempoRefrescoEventos.Value;
            if (tiempoRefrescoTareas.HasValue)
                TiempoRefrescoTareas = tiempoRefrescoTareas.Value;
            return this;
        }

        public void Build()
        {
            _services.AddSingleton(this);

            if (UsarCola || UsarEventDriven)
            {
                _services.AddTransient<IAlmacenColaComandos>(sp =>
                    new AlmacenColaComandos(_connectionString));
            }

            if (UsarCola)
            {
                _services.AddSingleton<IRegistroComandos<string, ResultadoComando>>(sp =>
                    new RegistroComandos<string, ResultadoComando>(_connectionString));

                _services.AddSingleton<IFactoriaComandos<string, ResultadoComando>, FactoriaComandos<string, ResultadoComando>>();

                _services.AddSingleton(sp =>
                    new ProcesadorColaComandos(
                        sp.GetRequiredService<IServiceScopeFactory>(),
                        MaxParalelismoCola,
                        TiempoRefrescoCola,
                        sp.GetRequiredService<ILogger<ProcesadorColaComandos>>()));

                _services.AddHostedService<ServicioColaComandos>();
            }

            if (UsarEventDriven)
            {
                _services.AddTransient<IColaEventos>(sp =>
                    new ColaEventos(_connectionString));

                _services.AddTransient<IRegistroManejadores>(sp =>
                    new RegistroManejadores(_connectionString));

                _services.AddSingleton<IRegistroTiposEvento>(sp =>
                    new RegistroTiposEvento(_connectionString));

                _services.AddScoped<ProcesadorEventos>();
                _services.AddSingleton<CoordinadorTareasProgramadas>();

                _services.AddHostedService<ServicioProcesadorEventos>();
                _services.AddHostedService<ServicioTareasProgramadas>();
            }
        }
    }
}
