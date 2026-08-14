using Microsoft.Extensions.Logging;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    public sealed class BusNotificacionEventosEnMemoria :
        IBusNotificacionEventos,
        IPublicadorNotificacionEventos
    {
        private readonly RegistroObservadoresNotificacion<NotificacionEventoLanzado> _observadores =
            new RegistroObservadoresNotificacion<NotificacionEventoLanzado>();
        private readonly ILoggerFactory _loggerFactory;

        public BusNotificacionEventosEnMemoria(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IObservadorNotificacionEvento Suscribir(string nombreEvento)
        {
            if (string.IsNullOrWhiteSpace(nombreEvento))
                throw new ArgumentException("El nombre del evento no puede estar vacío.", nameof(nombreEvento));

            return _observadores.Suscribir(
                nombreEvento,
                alDisponer => new ObservadorNotificacionEvento(
                    nombreEvento,
                    alDisponer,
                    _loggerFactory.CreateLogger<ObservadorNotificacionEvento>()));
        }

        public void Notificar(NotificacionEventoLanzado notificacion)
        {
            ArgumentNullException.ThrowIfNull(notificacion);
            _observadores.Notificar(notificacion.NombreEvento, notificacion);
        }
    }
}
