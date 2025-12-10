using Microsoft.Extensions.Hosting;
using PER.Comandos.LineaComandos.Cola.Procesadores;

namespace PER.Comandos.LineaComandos.Builder
{
    public class ServicioColaComandos : BackgroundService
    {
        private readonly ProcesadorColaComandos _procesador;

        public ServicioColaComandos(ProcesadorColaComandos procesador)
        {
            _procesador = procesador ?? throw new ArgumentNullException(nameof(procesador));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _procesador.StartAsync(stoppingToken);
        }
    }
}
