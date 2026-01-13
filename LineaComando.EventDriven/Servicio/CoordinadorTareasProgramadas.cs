using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using System.Collections.Concurrent;

namespace PER.Comandos.LineaComandos.EventDriven.Servicio
{
    /// <summary>
    /// Coordinador de tareas programadas (scheduled jobs).
    /// Lee configuración de handlers scheduled y encola comandos según expresión de intervalo (dd:hh:mm:ss).
    /// </summary>
    public class CoordinadorTareasProgramadas
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<CoordinadorTareasProgramadas> _logger;
        private readonly ConcurrentBag<Task> _concurrencyBag = new();

        public CoordinadorTareasProgramadas(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<CoordinadorTareasProgramadas> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Verifica y ejecuta las tareas programadas que correspondan.
        /// </summary>
        public virtual async Task EjecutarTareasProgramadasAsync(CancellationToken token = default)
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IRegistroManejadores registroManejadores = scope.ServiceProvider.GetRequiredService<IRegistroManejadores>();

            var manejadoresProgramados = await registroManejadores.ObtenerManejadoresProgramadosAsync(token);

            foreach (ConfiguracionManejador config in manejadoresProgramados.Where(m => m.Activo))
            {
                if (token.IsCancellationRequested)
                    break;

                if (DebeEjecutarse(config))
                {
                    _concurrencyBag.Add(EjecutarTareaAsync(config, token)
                        .ContinueWith(t =>
                        {
                            Task? retTaks = t;
                            _concurrencyBag.TryTake(out retTaks);
                        }));
                }
            }
        }

        /// <summary>
        /// Determina si una tarea debe ejecutarse basándose en su expresión de intervalo.
        /// </summary>
        protected virtual bool DebeEjecutarse(ConfiguracionManejador config)
        {
            if (string.IsNullOrEmpty(config.Expresion))
                return false;

            if (config.UltimaEjecucion.HasValue)
            {
                DateTime siguienteEjecucion = CalcularSiguienteEjecucion(config.Expresion, config.UltimaEjecucion.Value);
                return DateTime.UtcNow >= siguienteEjecucion;
            }

            return true;
        }

        /// <summary>
        /// Calcula la siguiente ejecución basándose en la expresión de intervalo.
        /// Formato: dd:hh:mm:ss (días:horas:minutos:segundos)
        /// Ejemplo: "00:01:30:00" = 1 hora y 30 minutos
        /// </summary>
        protected virtual DateTime CalcularSiguienteEjecucion(string expresion, DateTime ultimaEjecucion)
        {
            if (TryParseExpresion(expresion, out var intervalo))
            {
                return ultimaEjecucion.Add(intervalo);
            }

            // Por defecto, ejecutar cada hora si la expresión es inválida
            _logger.LogWarning("Expresión de intervalo inválida: {Expresion}. Usando intervalo por defecto de 1 hora.", expresion);
            return ultimaEjecucion.AddHours(1);
        }

        /// <summary>
        /// Parsea una expresión de intervalo en formato dd:hh:mm:ss
        /// </summary>
        protected virtual bool TryParseExpresion(string expresion, out TimeSpan intervalo)
        {
            intervalo = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(expresion))
                return false;

            var partes = expresion.Split(':');
            if (partes.Length != 4)
                return false;

            if (!int.TryParse(partes[0], out var dias) ||
                !int.TryParse(partes[1], out var horas) ||
                !int.TryParse(partes[2], out var minutos) ||
                !int.TryParse(partes[3], out var segundos))
            {
                return false;
            }

            if (dias < 0 || horas < 0 || horas > 23 || minutos < 0 || minutos > 59 || segundos < 0 || segundos > 59)
                return false;

            intervalo = new TimeSpan(dias, horas, minutos, segundos);
            return intervalo > TimeSpan.Zero;
        }

        /// <summary>
        /// Encola el comando asociado a una tarea programada.
        /// </summary>
        protected virtual async Task EjecutarTareaAsync(ConfiguracionManejador config, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Encolando comando para tarea programada {ManejadorId}", config.IDManejador);

                using IServiceScope scope = _serviceScopeFactory.CreateScope();
                IAlmacenColaComandos almacenColaComandos = scope.ServiceProvider.GetRequiredService<IAlmacenColaComandos>();
                IRegistroManejadores registroManejadores = scope.ServiceProvider.GetRequiredService<IRegistroManejadores>();

                var comandoEnCola = new ComandoEnCola
                {
                    RutaComando = config.RutaComando,
                    Argumentos = config.ArgumentosComando ?? string.Empty,
                    DatosDeComando = "{}",
                    FechaCreacion = DateTime.UtcNow,
                    Estado = "Pendiente",
                    Intentos = 0
                };

                await almacenColaComandos.EncolarAsync(comandoEnCola, token);

                DateTime ahora = DateTime.UtcNow;
                await registroManejadores.ActualizarUltimaEjecucionAsync(config.Id, ahora, token);

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
