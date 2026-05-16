using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Procesadores;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioColaComandos : BackgroundService
    {
        private readonly ProcesadorColaComandos _procesador;
        private readonly ILogger<ServicioColaComandos> _logger;

        public ServicioColaComandos(
            ProcesadorColaComandos procesador,
            ILogger<ServicioColaComandos> logger)
        {
            _procesador = procesador ?? throw new ArgumentNullException(nameof(procesador));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _procesador.StartAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("ServicioColaComandos cancelado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ServicioColaComandos finalizó por un error no controlado.");
            }
        }
    }
}
