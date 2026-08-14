namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    public interface IBusNotificacionEventos
    {
        IObservadorNotificacionEvento Suscribir(string nombreEvento);
    }
}
