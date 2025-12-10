using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        internal string ConnectionString => _connectionString;
        internal IServiceCollection Services => _services;

        public LineaComandoBuilder(IServiceCollection services, string connectionString)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public LineaComandoBuilder ConColaComandos(
            TimeSpan? tiempoRefresco = null,
            int maxParalelismo = 4)
        {
            UsarCola = true;
            if (tiempoRefresco.HasValue)
                TiempoRefrescoCola = tiempoRefresco.Value;
            MaxParalelismoCola = maxParalelismo;
            return this;
        }

        public LineaComandoBuilder ConEventDriven(
            TimeSpan? tiempoRefrescoEventos = null,
            TimeSpan? tiempoRefrescoTareas = null)
        {
            UsarEventDriven = true;
            if (tiempoRefrescoEventos.HasValue)
                TiempoRefrescoEventos = tiempoRefrescoEventos.Value;
            if (tiempoRefrescoTareas.HasValue)
                TiempoRefrescoTareas = tiempoRefrescoTareas.Value;
            return this;
        }

        public void Build()
        {
            _services.AddSingleton(this);

            if (UsarCola)
            {
                _services.AddHostedService<ServicioColaComandos>();
            }

            if (UsarEventDriven)
            {
                _services.AddHostedService<ServicioProcesadorEventos>();
                _services.AddHostedService<ServicioTareasProgramadas>();
            }
        }
    }
}
