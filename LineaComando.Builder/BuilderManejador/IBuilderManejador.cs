using PER.Comandos.LineaComandos.BuilderDisparador;

namespace PER.Comandos.LineaComandos.BuilderManejador;

public interface IBuilderManejador
{
    IBuilderManejador New();
    IBuilderManejador Argumentos(string codigo, string nombre, string argumentosComando, string? descripcion);
    Task<IBuilderDisparadorComando> RegistrarAsync();
}