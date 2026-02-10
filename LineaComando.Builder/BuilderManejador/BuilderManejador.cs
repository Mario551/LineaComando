using PER.Comandos.LineaComandos.BuilderDisparador;
using PER.Comandos.LineaComandos.EventDriven.DAO;
using PER.Comandos.LineaComandos.EventDriven.Manejador;
using PER.Comandos.LineaComandos.Registro;

namespace PER.Comandos.LineaComandos.BuilderManejador;

public class BuilderManejador : IBuilderManejador
{
    private MetadatosComando _metadatosComando;
    private IServiceProvider _service;
    private string _codigo = string.Empty;
    private string _nombre = string.Empty;
    private string _argumentosComando = string.Empty;
    private string? _descripcion;
    private bool _argumentosInicializados;

    public BuilderManejador(MetadatosComando metadatosComando, IServiceProvider service)
    {
        _metadatosComando = metadatosComando;
        _service = service;
    }

    public IBuilderManejador New()
        => new BuilderManejador(_metadatosComando, _service);

    public IBuilderManejador Argumentos(string codigo, string nombre, string argumentosComando, string? descripcion)
    {
        _codigo = codigo;
        _nombre = nombre;
        _argumentosComando = argumentosComando;
        _descripcion = descripcion;
        _argumentosInicializados = true;
        return this;
    }

    public async Task<IBuilderDisparadorComando> RegistrarAsync()
    {
        if (!_argumentosInicializados)
            throw new InvalidOperationException("Debe llamar a Argumentos() antes de RegistrarAsync()");

        var registroManejadores = _service.GetService(typeof(IRegistroManejadores)) as IRegistroManejadores
            ?? throw new InvalidOperationException("IRegistroManejadores no está registrado en el ServiceProvider");

        var manejador = new ManejadorEvento
        {
            Codigo = _codigo,
            Nombre = _nombre,
            Descripcion = _descripcion,
            IdComandoRegistrado = _metadatosComando.Id,
            RutaComando = _metadatosComando.RutaComando,
            ArgumentosComando = _argumentosComando,
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        await registroManejadores.RegistrarManejadorAsync(manejador);

        return new BuilderDisparador.BuilderDisparador();
    }
}