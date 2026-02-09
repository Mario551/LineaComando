using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.BuilderManejador;
using PER.Comandos.LineaComandos.Comando;

namespace PER.Comandos.LineaComandos.BuilderComando;

public interface IBuilderComando
{
    IBuilderComando Argumentos(string rutaComando, string? descripcion);
    IBuilderComando Accion<TRead, TWrite>(Func<ICollection<Parametro>, IComando<TRead, TWrite>> accion);
    Task<IBuilderManejador> RegistrarAsync();
}


