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
    }
}
