using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderDisparador;

public interface IBuilderDisparadorComando
{
    IBuilderDisparadorComando New();
    IBuilderDisparadorComando Argumentos(string codigo, string nombre, int prioridad, ITipoEvento eventoBD);
    IBuilderDisparadorComando Argumentos(string codigo, string nombre, int prioridad, string expresion);
    Task RegistrarAsync();
}