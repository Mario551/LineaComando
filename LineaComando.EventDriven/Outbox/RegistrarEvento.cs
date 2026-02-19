using Microsoft.Extensions.DependencyInjection;

namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public class RegistrarEvento : IRegistrarEvento
    {
        private IServiceProvider _serviceProvider;

        public RegistrarEvento(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IRegistroEventoBuilder Evento()
            => new RegistroEventoBuilder(_serviceProvider.GetRequiredService<IColaEventos>());
    }
}