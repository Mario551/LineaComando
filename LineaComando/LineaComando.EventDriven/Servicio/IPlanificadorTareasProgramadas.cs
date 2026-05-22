namespace PER.Comandos.LineaComandos.EventDriven.Servicio
{
    public interface IPlanificadorTareasProgramadas
    {
        Task IniciarAsync(CancellationToken token = default);
    }
}
