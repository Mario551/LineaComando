using PER.Comandos.LineaComandos.Cola.Resultados;

namespace PER.Comandos.LineaComandos.Cola.Almacen
{
    /// <summary>
    /// Acceso a cola de comandos.
    /// </summary>
    public interface IAlmacenColaComandos
    {
        /// <summary>
        /// Encola un nuevo comando.
        /// </summary>
        Task<long> EncolarAsync(ComandoEnCola comando, CancellationToken token = default);

        /// <summary>
        /// Obtiene comandos pendientes de procesar.
        /// </summary>
        Task<IEnumerable<ComandoEnCola>> ObtenerComandosPendientesAsync(int tamanioLote = 50, CancellationToken token = default);

        /// <summary>
        /// Marca comandos como procesando.
        /// </summary>
        Task<IEnumerable<ComandoEnCola>> MarcarComandosProcesandoAsync(long[] ids, CancellationToken token = default);

        /// <summary>
        /// Marca un comando como procesado con su resultado; éxito o error
        /// </summary>
        Task MarcarComoProcesadoAsync(long comandoId, ResultadoComando resultado, CancellationToken token = default);

        Task MarcarComoProcesadoAsync(
            long comandoId,
            ResultadoComando resultado,
            PayloadResultadoComando? payloadResultado,
            CancellationToken token = default);

        Task<ResultadoComandoPersistido?> ObtenerResultadoPersistidoAsync(
            long comandoId,
            CancellationToken token = default);

        /// <summary>
        /// Actualiza la fecha de lectura de los comandos especificados.
        /// </summary>
        Task ActualizarFechaLeidoAsync(long[] ids, CancellationToken token = default);
    }
}
