using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Registro
{
    public interface IRegistroTiposEvento
    {
        Task<int> RegistrarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default);

        Task<TipoEvento?> ObtenerTipoEventoPorCodigoAsync(string codigo, CancellationToken token = default);

        Task<TipoEvento?> ObtenerTipoEventoPorIdAsync(int id, CancellationToken token = default);

        Task<IEnumerable<TipoEvento>> ObtenerTiposEventosActivosAsync(CancellationToken token = default);

        Task ActualizarTipoEventoAsync(TipoEvento tipoEvento, CancellationToken token = default);

        Task DesactivarTipoEventoAsync(int id, CancellationToken token = default);
    }
}
