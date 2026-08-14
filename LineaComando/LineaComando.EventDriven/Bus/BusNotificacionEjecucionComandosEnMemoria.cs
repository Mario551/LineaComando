using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Cola.Notificaciones;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    public sealed class BusNotificacionEjecucionComandosEnMemoria :
        IBusNotificacionEjecucionComandos,
        IPublicadorNotificacionEjecucionComandos
    {
        private readonly RegistroObservadoresNotificacion<NotificacionEjecucionComando> _observadores =
            new RegistroObservadoresNotificacion<NotificacionEjecucionComando>();
        private readonly ILoggerFactory _loggerFactory;

        public BusNotificacionEjecucionComandosEnMemoria(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IObservadorNotificacionEjecucionComando Suscribir(string rutaComando)
        {
            if (string.IsNullOrWhiteSpace(rutaComando))
                throw new ArgumentException("La ruta del comando no puede estar vacía.", nameof(rutaComando));

            return _observadores.Suscribir(
                rutaComando,
                alDisponer => new ObservadorNotificacionEjecucionComando(
                    rutaComando,
                    alDisponer,
                    _loggerFactory.CreateLogger<ObservadorNotificacionEjecucionComando>()));
        }

        public void Notificar(NotificacionEjecucionComando notificacion)
        {
            ArgumentNullException.ThrowIfNull(notificacion);
            _observadores.Notificar(notificacion.RutaComando, notificacion);
        }
    }
}
