namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public interface IRegistrarEventoBuilder
    {
        IRegistrarEvento NewEvento();
    }
}