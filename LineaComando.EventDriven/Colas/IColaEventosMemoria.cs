using PER.Comandos.LineaComandos.EventDriven.Outbox;

namespace PER.Comandos.LineaComandos.EventDriven.Colas
{
    public interface IColaEventosMemoria
    {
        Task CargarPendientesDesdeBaseDatosAsync(CancellationToken token = default);

        Task EncolarAsync(EventoOutbox evento, CancellationToken token = default);

        IAsyncEnumerable<EventoOutbox> LeerAsync(CancellationToken token = default);
    }
}
