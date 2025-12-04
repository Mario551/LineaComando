using PER.Comandos.LineaComandos.EventDriven.DAO;

namespace PER.Comandos.LineaComandos.EventDriven.Manejador
{
    /// <summary>
    /// Registro y configuración de handlers.
    /// </summary>
    public interface IRegistroManejadores
    {
        Task<int> RegistrarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default);

        Task<ManejadorEvento?> ObtenerManejadorPorIdAsync(int id, CancellationToken token = default);

        Task<ManejadorEvento?> ObtenerManejadorPorCodigoAsync(string codigo, CancellationToken token = default);

        Task<IEnumerable<ManejadorEvento>> ObtenerManejadoresActivosAsync(CancellationToken token = default);

        Task ActualizarManejadorAsync(ManejadorEvento manejador, CancellationToken token = default);

        Task DesactivarManejadorAsync(int id, CancellationToken token = default);

        Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresParaEventoAsync(string tipoEvento, CancellationToken token = default);

        Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresProgramadosAsync(CancellationToken token = default);

        Task ActualizarConfiguracionAsync(ConfiguracionManejador configuracion, CancellationToken token = default);
    }
}
