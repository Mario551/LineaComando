using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderDisparador;

public class BuilderDisparador : IBuilderDisparadorComando
{
    public BuilderDisparador()
    {
    }

    public IBuilderDisparadorComando New()
    {
        throw new NotImplementedException();
    }

    public IBuilderDisparadorComando Argumentos(string codigo, int prioridad, ITipoEvento evento)
    {
        throw new NotImplementedException();
    }

    public IBuilderDisparadorComando Argumentos(string codigo, int prioridad, string expresion)
    {
        throw new NotImplementedException();
    }

    public Task RegistrarAsync()
    {
        throw new NotImplementedException();
    }
}