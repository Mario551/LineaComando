using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Colas;
using PER.Comandos.LineaComandos.EventDriven.Manejador;

namespace PER.Comandos.LineaComandos.EventDriven.Servicio
{
    /// <summary>
    /// Coordinador de tareas programadas (scheduled jobs).
    /// Lee configuración de handlers scheduled y encola comandos según expresión de intervalo (dd:hh:mm:ss).
    /// </summary>
    public class CoordinadorTareasProgramadas : IPlanificadorTareasProgramadas
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

                if (ExpresionProgramadaVacia(config))
                    continue;

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

        public virtual async Task IniciarAsync(CancellationToken token = default)
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IRegistroManejadores registroManejadores = scope.ServiceProvider.GetRequiredService<IRegistroManejadores>();

            IEnumerable<ConfiguracionManejador> manejadoresProgramados = await registroManejadores.ObtenerManejadoresProgramadosAsync(token);
            List<Task> tareasProgramadas = new List<Task>();

            foreach (ConfiguracionManejador config in manejadoresProgramados.Where(m => m.Activo))
            {
                if (ExpresionProgramadaVacia(config))
                    continue;

                tareasProgramadas.Add(EjecutarPlanificacionAsync(config, token));
            }

            if (!tareasProgramadas.Any())
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return;
            }

            await Task.WhenAll(tareasProgramadas);
        }

        private async Task EjecutarPlanificacionAsync(ConfiguracionManejador config, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TimeSpan espera = CalcularEspera(config);

                if (espera == Timeout.InfiniteTimeSpan)
                {
                    ExpresionProgramadaVacia(config);
                    return;
                }

                if (espera > TimeSpan.Zero)
                    await Task.Delay(espera, token);

                await EjecutarTareaAsync(config, token);
                config.UltimaEjecucion = DateTime.Now;
            }
        }

        private TimeSpan CalcularEspera(ConfiguracionManejador config)
        {
            if (string.IsNullOrWhiteSpace(config.Expresion))
                return Timeout.InfiniteTimeSpan;

            if (!config.UltimaEjecucion.HasValue)
                return TimeSpan.Zero;

            string expresion = config.Expresion!;
            DateTime siguienteEjecucion = CalcularSiguienteEjecucion(expresion, config.UltimaEjecucion.Value);
            DateTime ahora = DateTime.Now;

            if (ahora >= siguienteEjecucion)
                return TimeSpan.Zero;

            return siguienteEjecucion - ahora;
        }

        /// <summary>
        /// Determina si una tarea debe ejecutarse basándose en su expresión de intervalo.
        /// </summary>
        protected virtual bool DebeEjecutarse(ConfiguracionManejador config)
        {
            if (ExpresionProgramadaVacia(config))
                return false;

            if (config.UltimaEjecucion.HasValue)
            {
                string expresion = config.Expresion!;
                DateTime siguienteEjecucion = CalcularSiguienteEjecucion(expresion, config.UltimaEjecucion.Value);
                return DateTime.Now >= siguienteEjecucion;
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
                IColaComandosMemoria colaComandosMemoria = scope.ServiceProvider.GetRequiredService<IColaComandosMemoria>();
                IRegistroManejadores registroManejadores = scope.ServiceProvider.GetRequiredService<IRegistroManejadores>();

                StringBuilder sbArgumentos = new StringBuilder();
                sbArgumentos.Append("--origen=disparador");
                sbArgumentos.Append(" --codigo=" + config.Codigo);
                if (!string.IsNullOrEmpty(config.ArgumentosComando))
                    sbArgumentos.Append(" " + config.ArgumentosComando);
                string argumentos = sbArgumentos.ToString();

                SolicitudComando solicitud = new SolicitudComando
                {
                    RutaComando = config.RutaComando,
                    Argumentos = argumentos,
                    DatosDeComando = "{}"
                };

                await colaComandosMemoria.EncolarAsync(solicitud, token);

                DateTime ahora = DateTime.Now;
                await registroManejadores.ActualizarUltimaEjecucionAsync(config.Id, ahora, token);

                _logger.LogInformation("Comando encolado: {RutaComando} para tarea programada {ManejadorId}",
                    config.RutaComando, config.IDManejador);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.LogWarning("Tarea programada {ManejadorId} cancelada.", config.IDManejador);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encolando comando para tarea programada {ManejadorId}: {Error}",
                    config.IDManejador, ex.Message);
            }
        }

        private bool ExpresionProgramadaVacia(ConfiguracionManejador config)
        {
            if (!string.IsNullOrWhiteSpace(config.Expresion))
                return false;

            _logger.LogWarning(
                "Tarea programada {ManejadorId} no se planificó porque la expresión está vacía. Ruta: {RutaComando}, Código: {Codigo}.",
                config.IDManejador,
                config.RutaComando,
                config.Codigo);

            return true;
        }
    }
}
