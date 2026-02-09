using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.BuilderManejador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;

namespace PER.Comandos.LineaComandos.BuilderComando;

public class BuilderComando : IBuilderComando
{
    private string _rutaComando = string.Empty;
    private string? _descripcion;
    private Func<ICollection<Parametro>, IComando<string, ResultadoComando>>? _accion;

    private readonly IRegistroComandos<string, ResultadoComando> _registroComandos;

    public BuilderComando(IRegistroComandos<string, ResultadoComando> registroComandos)
    {
        _registroComandos = registroComandos;
    }

    public IBuilderComando Argumentos(string rutaComando, string? descripcion)
    {
        _rutaComando = rutaComando;
        _descripcion = descripcion;
        return this;
    }

    public IBuilderComando Accion<TRead, TWrite>(Func<ICollection<Parametro>, IComando<TRead, TWrite>> accion)
    {
        _accion = (parametros) => accion(parametros) as IComando<string, ResultadoComando>
            ?? throw new InvalidCastException("El comando debe implementar IComando<string, ResultadoComando>");
        return this;
    }

    public async Task<IBuilderManejador> RegistrarAsync()
    {
        var metadatos = new MetadatosComando
        {
            RutaComando = _rutaComando,
            Descripcion = _descripcion
        };

        var nodo = new Nodo<string, ResultadoComando>(_accion!);

        await _registroComandos.RegistrarComandoAsync(metadatos, nodo);

        return new BuilderManejador.BuilderManejador(0);
    }
}
