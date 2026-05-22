using PER.Comandos.LineaComandos.BuilderTipoEvento;
using PER.Comandos.LineaComandos.EventDriven.DAO;
using PER.Comandos.LineaComandos.EventDriven.Manejador;

namespace PER.Comandos.LineaComandos.BuilderDisparador;

public class BuilderDisparador : IBuilderDisparador
{
    private int _idManejador;
    private readonly IServiceProvider _service;
    private string _codigo = string.Empty;
    private int _prioridad;
    private ITipoEvento? _tipoEvento;
    private string? _expresion;
    private bool _argumentosInicializados;
    private bool _esModoEvento;

    public BuilderDisparador(int idManejador, IServiceProvider service)
    {
        _idManejador = idManejador;
        _service = service;
    }

    public IBuilderDisparador New()
    {
        return this;
    }

    public IBuilderDisparador Argumentos(string codigo, int prioridad, ITipoEvento evento)
    {
        _codigo = codigo;
        _prioridad = prioridad;
        _tipoEvento = evento;
        _expresion = null;
        _esModoEvento = true;
        _argumentosInicializados = true;
        return this;
    }

    public IBuilderDisparador Argumentos(string codigo, int prioridad, string expresion)
    {
        _codigo = codigo;
        _prioridad = prioridad;
        _expresion = expresion;
        _tipoEvento = null;
        _esModoEvento = false;
        _argumentosInicializados = true;
        return this;
    }

    public async Task RegistrarAsync()
    {
        if (!_argumentosInicializados)
            throw new InvalidOperationException("Debe llamar a Argumentos() antes de RegistrarAsync()");

        var registroManejadores = _service.GetService(typeof(IRegistroManejadores)) as IRegistroManejadores
            ?? throw new InvalidOperationException("IRegistroManejadores no está registrado en el ServiceProvider");

        var disparador = new DisparadorManejador
        {
            ManejadorEventoId = _idManejador,
            Codigo = _codigo,
            ModoDisparo = _esModoEvento ? "Evento" : "Programado",
            TipoEventoId = _tipoEvento?.ID,
            Expresion = _expresion,
            Prioridad = _prioridad,
            Activo = true,
            CreadoEn = DateTime.Now
        };

        await registroManejadores.RegistrarDisparadorAsync(disparador);
    }
}