using System.Runtime.CompilerServices;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    public interface IObservadorNotificacionEvento : IDisposable
    {
        event Func<NotificacionEventoLanzado, CancellationToken, Task>? EventoRecibido;

        Task<NotificacionEventoLanzado> EsperarAsync(
            CancellationToken cancellationToken = default);

        TaskAwaiter<NotificacionEventoLanzado> GetAwaiter();
    }
}
