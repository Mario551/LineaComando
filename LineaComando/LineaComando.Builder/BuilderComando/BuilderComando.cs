using Microsoft.Extensions.DependencyInjection;
using PER.Comandos.LineaComandos.Atributo;
using PER.Comandos.LineaComandos.BuilderManejador;
using PER.Comandos.LineaComandos.Cola.Almacen;
using PER.Comandos.LineaComandos.Cola.Resultados;
using PER.Comandos.LineaComandos.Cola.Registro;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.FactoriaComandos;
using PER.Comandos.LineaComandos.Registro;

namespace PER.Comandos.LineaComandos.BuilderComando;

public class BuilderComando : IBuilderComando
{
    private readonly string _nombreFactoria;
    private string _rutaComando = string.Empty;
    private string? _descripcion;
    private Func<ICollection<Parametro>, IComando<string, ResultadoComando>>? _accionFunc;
    private ComandoBase<string, ResultadoComando>? _accionComandoBase;
    private IProcesadorResultadoComando? _procesadorResultado;
    private bool _argumentosInicializados;
    private bool _accionInicializada;

    private readonly IServiceProvider _serviceProvider;

    public BuilderComando(IServiceProvider serviceProvider, string nombreFactoria)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreFactoria);
        _nombreFactoria = nombreFactoria;
    }

    public IBuilderComando New() => new BuilderComando(_serviceProvider, _nombreFactoria);

    public IBuilderComando Argumentos(string rutaComando, string? descripcion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaComando);

        string[] partesRuta = rutaComando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partesRuta.Any(parte => parte.StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException("La ruta relativa solo puede contener nombres de comandos.", nameof(rutaComando));

        _rutaComando = $"{_nombreFactoria} {string.Join(' ', partesRuta)}";
        _descripcion = descripcion;
        _argumentosInicializados = true;
        return this;
    }

    public IBuilderComando Accion(Func<ICollection<Parametro>, IComando<string, ResultadoComando>> accion)
    {
        _accionFunc = (parametros) => accion(parametros) as IComando<string, ResultadoComando>
            ?? throw new InvalidCastException("El comando debe implementar IComando<string, ResultadoComando>");

        _accionInicializada = true;
        return this;
    }

    public IBuilderComando Accion(ComandoBase<string, ResultadoComando> accion)
    {
        _accionComandoBase = accion;
        _accionInicializada = true;
        return this;
    }

    public IBuilderComando Resultado(IProcesadorResultadoComando procesador)
    {
        _procesadorResultado = procesador ?? throw new ArgumentNullException(nameof(procesador));
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

        Nodo<string, ResultadoComando> nodo;
        if (_accionFunc != null) 
            nodo = new Nodo<string, ResultadoComando>(_accionFunc!);
        else 
            nodo = new Nodo<string, ResultadoComando>(_accionComandoBase!);

        var registroComandos = _serviceProvider.GetRequiredService<IRegistroComandos<string, ResultadoComando>>();

        await registroComandos.RegistrarComandoAsync(metadatos, nodo);

        if (_procesadorResultado is not null)
        {
            IRegistroProcesadoresSerializacionResultadosComando registroProcesadoresSerializacionResultados =
                _serviceProvider.GetRequiredService<IRegistroProcesadoresSerializacionResultadosComando>();
            registroProcesadoresSerializacionResultados.Registrar(_rutaComando, _procesadorResultado);
        }

        return new BuilderManejador.BuilderManejador(metadatos, _serviceProvider);
    }
}
