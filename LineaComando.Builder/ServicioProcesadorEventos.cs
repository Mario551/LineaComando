using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.EventDriven.Servicio;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioProcesadorEventos : BackgroundService
    {
        private readonly ProcesadorEventos _procesador;
        private readonly TimeSpan _tiempoRefresco;

        public ServicioProcesadorEventos(ProcesadorEventos procesador, LineaComandoBuilder builder)
        {
            _procesador = procesador ?? throw new ArgumentNullException(nameof(procesador));
            _tiempoRefresco = builder.TiempoRefrescoEventos;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _procesador.ProcesarLoteAsync(50, stoppingToken);
                    await Task.Delay(_tiempoRefresco, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
