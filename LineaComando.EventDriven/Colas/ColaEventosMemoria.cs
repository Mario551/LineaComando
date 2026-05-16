using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace PER.Comandos.LineaComandos.EventDriven.Colas
{
    public sealed class ColaEventosMemoria : IColaEventosMemoria
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly Channel<EventoOutbox> _channel;

        public ColaEventosMemoria(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _channel = Channel.CreateUnbounded<EventoOutbox>();
        }

        public async Task CargarPendientesDesdeBaseDatosAsync(CancellationToken token = default)
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IColaEventos colaEventos = scope.ServiceProvider.GetRequiredService<IColaEventos>();

            IEnumerable<EventoOutbox> eventosPendientes = await colaEventos.ObtenerEventosPendientesAsync(
                int.MaxValue,
                token);

            foreach (EventoOutbox eventoPendiente in eventosPendientes)
            {
                await _channel.Writer.WriteAsync(eventoPendiente, token);
            }
        }

        public async Task EncolarAsync(EventoOutbox evento, CancellationToken token = default)
        {
            await _channel.Writer.WriteAsync(evento, token);
        }

        public IAsyncEnumerable<EventoOutbox> LeerAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAllAsync(token);
        }
    }
}
