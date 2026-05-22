using PER.Comandos.LineaComandos.EventDriven.Registro;

namespace PER.Comandos.LineaComandos.Builder
{
    public interface ILineaComandoConCola
    {
        ILineaComandoConCola ConEventDriven(
            Func<IServiceProvider, IRegistroTiposEvento, CancellationToken, Task> configuradorTiposEvento);

        void Build();
    }
}
