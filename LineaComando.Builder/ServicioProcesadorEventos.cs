using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.EventDriven.Servicio;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioProcesadorEventos : BackgroundService
    {
        private IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _tiempoRefresco;

        public ServicioProcesadorEventos(IServiceScopeFactory scopeFactory, LineaComandoBuilder builder)
        {
            _scopeFactory = scopeFactory;
            _tiempoRefresco = builder.TiempoRefrescoEventos;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                ProcesadorEventos procesador = scope.ServiceProvider.GetRequiredService<ProcesadorEventos>();

                try
                {
                    await procesador.ProcesarLoteAsync(500, stoppingToken);
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
