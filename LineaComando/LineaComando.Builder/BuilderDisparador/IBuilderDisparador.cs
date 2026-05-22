using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderDisparador;

public interface IBuilderDisparador
{
    IBuilderDisparador New();
    IBuilderDisparador Argumentos(string codigo, int prioridad, ITipoEvento eventoBD);
    IBuilderDisparador Argumentos(string codigo, int prioridad, string expresion);
    Task RegistrarAsync();
}