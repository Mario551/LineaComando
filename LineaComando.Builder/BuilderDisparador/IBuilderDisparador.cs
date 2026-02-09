using PER.Comandos.LineaComandos.BuilderEvento;

namespace PER.Comandos.LineaComandos.BuilderDisparador;

public interface IBuilderDisparadorComando
{
    IBuilderDisparadorComando Clear();
    IBuilderDisparadorComando Argumentos(string codigo, int prioridad, IEventoBD eventoBD);
    IBuilderDisparadorComando Argumentos(string codigo, int prioridad, string expresion);
    Task RegistrarAsync();
}