using System.Runtime.CompilerServices;

namespace PER.Comandos.LineaComandos.Cola.Notificaciones
{
    public interface IObservadorNotificacionEjecucionComando : IDisposable
    {
        event Func<NotificacionEjecucionComando, CancellationToken, Task>? NotificacionRecibida;

        Task<NotificacionEjecucionComando> EsperarAsync(
            CancellationToken cancellationToken = default);

        TaskAwaiter<NotificacionEjecucionComando> GetAwaiter();
    }
}
