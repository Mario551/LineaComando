using Microsoft.Extensions.Logging;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.Configuracion;

namespace PER.Comandos.LineaComandos.Comando
{
    public interface IComandoCreador<TRead, TWrite>
    {
        IComandoCreador<TRead, TWrite> EmpezarCon(Func<CancellationToken, Task> comenzar);
        IComandoCreador<TRead, TWrite> FinalizarCon(Func<CancellationToken, Task> finalizar);
        IComando<TRead, TWrite> Crear(Stack<string> lineaComando, ICollection<Parametro> parametros);
    }
}
