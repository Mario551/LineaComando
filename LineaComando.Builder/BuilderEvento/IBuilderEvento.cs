namespace PER.Comandos.LineaComandos.BuilderEvento;

public interface IBuilderEvento
{
    IBuilderEvento Argumentos(string codigo, string nombre, string? descripcion);
    Task<IEventoBD> RegistrarAsync();
}

public interface IEventoBD
{
    public int ID { get; }
}