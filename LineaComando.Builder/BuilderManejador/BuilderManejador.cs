using PER.Comandos.LineaComandos.BuilderDisparador;

namespace PER.Comandos.LineaComandos.BuilderManejador;

public class BuilderManejador : IBuilderManejador
{
    private int _idComando;

    public BuilderManejador(int idComando)
    {
        _idComando = idComando;
    }

    public IBuilderManejador New()
        => new BuilderManejador(_idComando);
    
    public IBuilderManejador Argumentos(string codigo, string nombre, string argumentosComando, string? descripcion)
    {
        throw new NotImplementedException();
    }

    public Task<IBuilderDisparadorComando> RegistrarAsync()
    {
        throw new NotImplementedException();
    }
}