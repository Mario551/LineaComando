using PER.Comandos.LineaComandos.Stream;

namespace PER.Comandos.LineaComandos.Comando
{
    public interface IComando<TRead, TWrite>
    {
        void EmpezarCon(Func<CancellationToken, Task> comenzar);
        void FinalizarCon(Func<CancellationToken, Task> finalizar);
        Task EjecutarAsync(IStream<TRead, TWrite> stream, CancellationToken token = default);
    }
}
