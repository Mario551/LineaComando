using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    internal sealed class ObservadorNotificacionEvento :
        ObservadorNotificacionEnMemoria<NotificacionEventoLanzado>,
        IObservadorNotificacionEvento
    {
        public ObservadorNotificacionEvento(
            string nombreEvento,
            Action<ObservadorNotificacionEnMemoria<NotificacionEventoLanzado>> alDisponer,
            ILogger<ObservadorNotificacionEvento> logger)
            : base(nombreEvento, "evento", alDisponer, logger)
        {
        }

        public event Func<NotificacionEventoLanzado, CancellationToken, Task>? EventoRecibido
        {
            add
            {
                ArgumentNullException.ThrowIfNull(value);
                AgregarCallback(value);
            }
            remove
            {
                if (value is not null)
                    QuitarCallback(value);
            }
        }

        public async Task<NotificacionEventoLanzado> EsperarAsync(
            CancellationToken cancellationToken = default)
        {
            return await EsperarInternoAsync(cancellationToken);
        }

        public TaskAwaiter<NotificacionEventoLanzado> GetAwaiter()
        {
            return EsperarAsync().GetAwaiter();
        }
    }
}
