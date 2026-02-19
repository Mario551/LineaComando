namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public interface IRegistrarEvento
    {
        IRegistroEventoBuilder Evento();
    }
}