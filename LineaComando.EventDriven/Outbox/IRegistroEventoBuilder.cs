namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    public interface IRegistroEventoBuilder
    {
        IRegistroEventoBuilder Argumentos<TDato>(string tipoEvento, TDato datos, long? agregadoId);
        Task RegistrarEnColaAsync();
    }
}