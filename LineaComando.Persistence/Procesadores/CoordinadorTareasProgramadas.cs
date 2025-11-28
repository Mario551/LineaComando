using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.EventDriven.Handler;
using PER.Comandos.LineaComandos.EventSourcing.Registro;
using System.Collections.Concurrent;
using System.Threading.Task;

namespace PER.Comandos.LineaComandos.QueueCommand.Procesadores
{
    /// <summary>
    /// Coordinador de tareas programadas (scheduled jobs).
    /// Lee configuración de handlers scheduled y los ejecuta según cron.
    /// </summary>
    public class CoordinadorTareasProgramadas
    {
        private readonly IRegistroManejadores _registroManejadores;
        private readonly ILogger _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly Dictionary<int, DateTime> _ultimasEjecuciones = new();
        private readonly ConcurrencyBag<Task> _concurrencyBag = new();

        public CoordinadorTareasProgramadas(
            IRegistroManejadores registroManejadores,
            ILogger logger,
            IServiceProvider? serviceProvider = null)
        {
            _registroManejadores = registroManejadores;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Verifica y ejecuta las tareas programadas que correspondan.
        /// </summary>
        public virtual async Task EjecutarTareasProgramadasAsync(CancellationToken token = default)
        {
            var manejadoresProgramados = await _registroManejadores.ObtenerManejadoresProgramadosAsync(token);

            foreach (ConfiguracionManejador config in manejadoresProgramados.Where(m => m.Activo))
            {
                if (token.IsCancellationRequested)
                    break;

                if (DebeEjecutarse(config))
                {
                    _concurrencyBag.Add(EjecutarTareaAsync(config, token)
                        .ContinueWith(t => _concurrencyBag.TryTake(t)));
                }
            }
        }

        /// <summary>
        /// Determina si una tarea debe ejecutarse basándose en su expresión cron.
        /// </summary>
        protected virtual bool DebeEjecutarse(ConfiguracionManejador config)
        {
            if (string.IsNullOrEmpty(config.ExpresionCron))
                return false;

            // Verificar última ejecución
            if (_ultimasEjecuciones.TryGetValue(config.Id, out var ultimaEjecucion))
            {
                DateTime siguienteEjecucion = CalcularSiguienteEjecucion(config.ExpresionCron, ultimaEjecucion);
                return DateTime.UtcNow >= siguienteEjecucion;
            }

            return true;
        }

        /// <summary>
        /// Calcula la siguiente ejecución basándose en la expresión cron.
        /// Implementación simplificada - el usuario puede usar Quartz.NET para expresiones complejas.
        /// </summary>
        protected virtual DateTime CalcularSiguienteEjecucion(string expresionCron, DateTime ultimaEjecucion)
        {
            // Implementación simplificada para intervalos básicos
            // Formato simplificado: "INTERVALO:MINUTOS" o cron estándar
            if (expresionCron.StartsWith("INTERVALO:"))
            {
                if (int.TryParse(expresionCron.Replace("INTERVALO:", ""), out var minutos))
                {
                    return ultimaEjecucion.AddMinutes(minutos);
                }
            }

            // Para expresiones cron complejas, el usuario debería usar Quartz.NET
            // Por defecto, ejecutar cada hora
            return ultimaEjecucion.AddHours(1);
        }

        /// <summary>
        /// Ejecuta una tarea programada.
        /// </summary>
        protected virtual async Task EjecutarTareaAsync(ConfiguracionManejador config, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Ejecutando tarea programada {ManejadorId}", config.ManejadorId);

                // Aquí el usuario implementaría la instanciación del handler
                // usando el service provider

                _ultimasEjecuciones[config.Id] = DateTime.UtcNow;

                _logger.LogInformation("Tarea programada {ManejadorId} ejecutada exitosamente", config.ManejadorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando tarea programada {ManejadorId}: {Error}",
                    config.ManejadorId, ex.Message);
            }
        }
    }
}
