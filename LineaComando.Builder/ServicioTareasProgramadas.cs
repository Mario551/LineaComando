using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.EventDriven.Servicio;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioTareasProgramadas : BackgroundService
    {
        private readonly CoordinadorTareasProgramadas _coordinador;
        private readonly TimeSpan _tiempoRefresco;

        public ServicioTareasProgramadas(CoordinadorTareasProgramadas coordinador, LineaComandoBuilder builder)
        {
            _coordinador = coordinador ?? throw new ArgumentNullException(nameof(coordinador));
            _tiempoRefresco = builder.TiempoRefrescoTareas;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _coordinador.EjecutarTareasProgramadasAsync(stoppingToken);
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
