using PER.Comandos.LineaComandos.Cola.Almacen;

namespace PER.Comandos.LineaComandos.Cola.Colas
{
    public interface IColaComandosMemoria
    {
        Task CargarPendientesDesdeBaseDatosAsync(CancellationToken token = default);

        Task<ComandoEncolado> EncolarAsync(SolicitudComando solicitud, CancellationToken token = default);

        Task<ComandoEncolado> EsperarComandoAsync(long comandoId, CancellationToken token = default);

        IAsyncEnumerable<ComandoEnCola> LeerAsync(CancellationToken token = default);

        void CompletarResultado(long comandoId, ResultadoComando resultado);
    }
}
