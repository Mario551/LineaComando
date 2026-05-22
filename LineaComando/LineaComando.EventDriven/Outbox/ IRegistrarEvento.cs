namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public interface IRegistrarEvento
    {
        IRegistrarEvento Argumentos<TDato>(string tipoEvento, TDato datos, long? agregadoId = null);
        Task RegistrarEnColaAsync();
    }
}