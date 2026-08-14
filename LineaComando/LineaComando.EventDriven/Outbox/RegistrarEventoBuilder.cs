using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.EventDriven.Bus;
using PER.Comandos.LineaComandos.EventDriven.Colas;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class RegistrarEventoBuilder : IRegistrarEventoBuilder 
    {
        private IServiceProvider _serviceProvider;

        public RegistrarEventoBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IRegistrarEvento NewEvento()
            => new RegistrarEvento(
                _serviceProvider.GetRequiredService<IColaEventos>(),
                _serviceProvider.GetRequiredService<IColaEventosMemoria>(),
                _serviceProvider.GetRequiredService<IPublicadorNotificacionEventos>(),
                _serviceProvider.GetRequiredService<ILogger<RegistrarEvento>>());
    }
}
