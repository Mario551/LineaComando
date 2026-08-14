namespace PER.Comandos.LineaComandos.Cola.Notificaciones
{
    public interface IBusNotificacionEjecucionComandos
    {
        IObservadorNotificacionEjecucionComando Suscribir(string rutaComando);
    }
}
