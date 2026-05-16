using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.EventDriven.Servicio;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioTareasProgramadas : BackgroundService
    {
        private readonly IPlanificadorTareasProgramadas _planificador;
        private readonly ILogger<ServicioTareasProgramadas> _logger;

        public ServicioTareasProgramadas(
            IPlanificadorTareasProgramadas planificador,
            ILogger<ServicioTareasProgramadas> logger)
        {
            _planificador = planificador ?? throw new ArgumentNullException(nameof(planificador));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _planificador.IniciarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("ServicioTareasProgramadas cancelado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ServicioTareasProgramadas finalizó por un error no controlado.");
            }
        }
    }
}
