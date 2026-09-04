using PER.Comandos.LineaComandos.BuilderInicializador;

namespace PER.Comandos.LineaComandos.Builder;

internal sealed record ConfiguracionFactoriaComandos(
    string Nombre,
    Func<IServiceProvider, IBuilderInicializador, CancellationToken, Task> Configurar);
