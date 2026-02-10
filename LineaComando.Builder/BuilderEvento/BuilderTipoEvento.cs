using PER.Comandos.LineaComandos.EventDriven.DAO;
using PER.Comandos.LineaComandos.EventDriven.Registro;

namespace PER.Comandos.LineaComandos.BuilderTipoEvento;

public class BuilderTipoEvento : IBuilderTipoEvento
{
    private readonly IServiceProvider _service;
    private string _codigo = string.Empty;
    private string _nombre = string.Empty;
    private string? _descripcion;
    private bool _argumentosInicializados;

    public BuilderTipoEvento(IServiceProvider service)
    {
        _service = service;
    }

    public IBuilderTipoEvento Argumentos(string codigo, string nombre, string? descripcion)
    {
        _codigo = codigo;
        _nombre = nombre;
        _descripcion = descripcion;
        _argumentosInicializados = true;
        return this;
    }

    public async Task<ITipoEvento> RegistrarAsync()
    {
        if (!_argumentosInicializados)
        {
            throw new InvalidOperationException("Debe llamar a Argumentos() antes de RegistrarAsync()");
        }

        var registroTiposEvento = _service.GetService(typeof(IRegistroTiposEvento)) as IRegistroTiposEvento
            ?? throw new InvalidOperationException("IRegistroTiposEvento no está registrado en el ServiceProvider");

        var tipoEvento = new TipoEvento
        {
            Codigo = _codigo,
            Nombre = _nombre,
            Descripcion = _descripcion,
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        await registroTiposEvento.RegistrarTipoEventoAsync(tipoEvento);

        return new TipoEventoResult(tipoEvento.Id);
    }

    private class TipoEventoResult : ITipoEvento
    {
        public int ID { get; }

        public TipoEventoResult(int id)
        {
            ID = id;
        }
    }
}
