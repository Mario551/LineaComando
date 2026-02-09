namespace PER.Comandos.LineaComandos.BuilderDisparador;

class BuilderDisparador : IBuilderDisparadorComando
{
    public IBuilderDisparadorComando Clear()
    {
        throw new NotImplementedException();
    }

    public IBuilderDisparadorComando Argumentos(string codigo, int prioridad, BuilderEvento.IEventoBD eventoBD)
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