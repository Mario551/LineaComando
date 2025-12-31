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
        /// Determina si una tarea debe ejecutarse basándose en su expresión de intervalo.
        /// </summary>
        protected virtual bool DebeEjecutarse(ConfiguracionManejador config)
        {
            if (string.IsNullOrEmpty(config.Expresion))
                return false;

            // Verificar última ejecución
            if (_ultimasEjecuciones.TryGetValue(config.Id, out var ultimaEjecucion))
            {
                DateTime siguienteEjecucion = CalcularSiguienteEjecucion(config.Expresion, ultimaEjecucion);
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
