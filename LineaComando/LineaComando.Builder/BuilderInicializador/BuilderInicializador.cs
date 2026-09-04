using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderInicializador;

public class BuilderInicializador : IBuilderInicializador
{
    private readonly IServiceProvider _service;
    private readonly string _nombreFactoria;

    public BuilderInicializador(IServiceProvider service, string nombreFactoria)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreFactoria);
        _nombreFactoria = nombreFactoria;
    }

    public IBuilderComando NewBuilderComando()
        => new BuilderComando.BuilderComando(_service, _nombreFactoria);

    public IBuilderTipoEvento NewBuilderTipoEvento()
        => new BuilderTipoEvento.BuilderTipoEvento(_service);
}
