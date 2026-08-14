namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    public interface IPublicadorNotificacionEventos
    {
        void Notificar(NotificacionEventoLanzado notificacion);
    }
}
