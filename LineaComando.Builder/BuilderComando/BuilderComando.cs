using Microsoft.Extensions.DependencyInjection;
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
    private bool _argumentosInicializados;
    private bool _accionInicializada;

    private readonly IServiceProvider _serviceProvider;

    public BuilderComando(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IBuilderComando Argumentos(string rutaComando, string? descripcion)
    {
        _rutaComando = rutaComando;
        _descripcion = descripcion;
        _argumentosInicializados = true;
        return this;
    }

    public IBuilderComando Accion<TRead, TWrite>(Func<ICollection<Parametro>, IComando<TRead, TWrite>> accion)
    {
        _accion = (parametros) => accion(parametros) as IComando<string, ResultadoComando>
            ?? throw new InvalidCastException("El comando debe implementar IComando<string, ResultadoComando>");
        _accionInicializada = true;
        return this;
    }

    public async Task<IBuilderManejador> RegistrarAsync()
    {
        if (!_argumentosInicializados)
            throw new InvalidOperationException("Debe llamar a Argumentos() antes de RegistrarAsync()");

        if (!_accionInicializada)
            throw new InvalidOperationException("Debe llamar a Accion() antes de RegistrarAsync()");

        var metadatos = new MetadatosComando
        {
            RutaComando = _rutaComando,
            Descripcion = _descripcion
        };

        var nodo = new Nodo<string, ResultadoComando>(_accion!);

        var registroComandos = _serviceProvider.GetRequiredService<IRegistroComandos<string, ResultadoComando>>();

        await registroComandos.RegistrarComandoAsync(metadatos, nodo);

        return new BuilderManejador.BuilderManejador(metadatos, _serviceProvider);
    }
}
