using PER.Comandos.LineaComandos.Cola.Notificaciones;

namespace ComandosColaTest.Helpers
{
    public sealed class PublicadorNotificacionEjecucionComandosPrueba :
        IPublicadorNotificacionEjecucionComandos
    {
        private readonly object _sincronizacion = new object();
        private readonly List<NotificacionEjecucionComando> _notificaciones =
            new List<NotificacionEjecucionComando>();

        public IReadOnlyList<NotificacionEjecucionComando> Notificaciones
        {
            get
            {
                lock (_sincronizacion)
                {
                    return _notificaciones.ToArray();
                }
            }
        }

        public void Notificar(NotificacionEjecucionComando notificacion)
        {
            lock (_sincronizacion)
            {
                _notificaciones.Add(notificacion);
            }
        }
    }
}
