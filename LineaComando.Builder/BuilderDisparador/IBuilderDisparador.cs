using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderDisparador;

public interface IBuilderDisparadorComando
{
    IBuilderDisparadorComando New();
    IBuilderDisparadorComando Argumentos(string codigo, int prioridad, ITipoEvento eventoBD);
    IBuilderDisparadorComando Argumentos(string codigo, int prioridad, string expresion);
    Task RegistrarAsync();
}