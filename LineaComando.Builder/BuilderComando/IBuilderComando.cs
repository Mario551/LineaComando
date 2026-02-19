using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.BuilderManejador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Comando;

namespace PER.Comandos.LineaComandos.BuilderComando;

public interface IBuilderComando
{
    IBuilderComando New();
    IBuilderComando Argumentos(string rutaComando, string? descripcion);
    IBuilderComando Accion(Func<ICollection<Parametro>, IComando<string, ResultadoComando>> accion);
    IBuilderComando Accion(ComandoBase<string, ResultadoComando> accion);
    Task<IBuilderManejador> RegistrarAsync();
}


