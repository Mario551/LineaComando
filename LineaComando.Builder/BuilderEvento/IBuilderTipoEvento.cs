namespace PER.Comandos.LineaComandos.BuilderTipoEvento;

public interface IBuilderTipoEvento
{
    IBuilderTipoEvento Argumentos(string codigo, string nombre, string? descripcion);
    Task<ITipoEvento> RegistrarAsync();
}
