using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Notificaciones;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    internal sealed class ObservadorNotificacionEjecucionComando :
        ObservadorNotificacionEnMemoria<NotificacionEjecucionComando>,
        IObservadorNotificacionEjecucionComando
    {
        public ObservadorNotificacionEjecucionComando(
            string rutaComando,
            Action<ObservadorNotificacionEnMemoria<NotificacionEjecucionComando>> alDisponer,
            ILogger<ObservadorNotificacionEjecucionComando> logger)
            : base(rutaComando, "ejecución de comando", alDisponer, logger)
        {
        }

        public event Func<NotificacionEjecucionComando, CancellationToken, Task>? NotificacionRecibida
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

        public async Task<NotificacionEjecucionComando> EsperarAsync(
            CancellationToken cancellationToken = default)
        {
            return await EsperarInternoAsync(cancellationToken);
        }

        public TaskAwaiter<NotificacionEjecucionComando> GetAwaiter()
        {
            return EsperarAsync().GetAwaiter();
        }
    }
}
