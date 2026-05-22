using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderInicializador;

public class BuilderInicializador : IBuilderInicializador
{
    private IServiceProvider _service;

    public BuilderInicializador(IServiceProvider service)
    {
        _service = service;
    }

    public IBuilderComando NewBuilderComando()
        => new BuilderComando.BuilderComando(_service);

    public IBuilderTipoEvento NewBuilderTipoEvento()
        => new BuilderTipoEvento.BuilderTipoEvento(_service);
}