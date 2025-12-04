using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using System.Collections.Concurrent;

namespace PER.Comandos.LineaComandos.EventDriven.Servicio
{
    /// <summary>
    /// Coordinador de tareas programadas (scheduled jobs).
    /// Lee configuración de handlers scheduled y encola comandos según cron.
    /// </summary>
    public class CoordinadorTareasProgramadas
    {
        private readonly IRegistroManejadores _registroManejadores;
        private readonly IAlmacenColaComandos _almacenColaComandos;
        private readonly ILogger _logger;
        private readonly Dictionary<int, DateTime> _ultimasEjecuciones = new();
        private readonly ConcurrentBag<Task> _concurrencyBag = new();

        public CoordinadorTareasProgramadas(
            IRegistroManejadores registroManejadores,
            IAlmacenColaComandos almacenColaComandos,
            ILogger logger)
        {
            _registroManejadores = registroManejadores ?? throw new ArgumentNullException(nameof(registroManejadores));
            _almacenColaComandos = almacenColaComandos ?? throw new ArgumentNullException(nameof(almacenColaComandos));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                        .ContinueWith(t => _concurrencyBag.TryTake(out t)));
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
        /// Encola el comando asociado a una tarea programada.
        /// </summary>
        protected virtual async Task EjecutarTareaAsync(ConfiguracionManejador config, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Encolando comando para tarea programada {ManejadorId}", config.IDManejador);

                var comandoEnCola = new ComandoEnCola
                {
                    RutaComando = config.RutaComando,
                    Argumentos = config.ArgumentosComando ?? string.Empty,
                    DatosDeComando = string.Empty,
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "Pendiente",
                    Intentos = 0
                };

                await _almacenColaComandos.EncolarAsync(comandoEnCola, token);

                _ultimasEjecuciones[config.Id] = DateTime.UtcNow;

                _logger.LogInformation("Comando encolado: {RutaComando} para tarea programada {ManejadorId}",
                    config.RutaComando, config.IDManejador);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encolando comando para tarea programada {ManejadorId}: {Error}",
                    config.IDManejador, ex.Message);
            }
        }
    }
}
