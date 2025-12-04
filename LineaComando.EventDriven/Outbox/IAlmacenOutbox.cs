namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    /// <summary>
    /// Responsabilidad: Persistir eventos (Outbox pattern).
    /// El usuario implementa con su BD preferida.
    /// </summary>
    public interface IAlmacenOutbox
    {
        /// <summary>
        /// Guarda un evento en el outbox.
        /// </summary>
        Task<long> GuardarEventoAsync(DatosEvento datosEvento, CancellationToken token = default);

        /// <summary>
        /// Obtiene eventos pendientes de procesar.
        /// </summary>
        Task<IEnumerable<EventoOutbox>> ObtenerEventosPendientesAsync(int tamanioLote = 50, CancellationToken token = default);

        /// <summary>
        /// Marca un evento como procesado.
        /// </summary>
        Task MarcarComoProcesadoAsync(long eventoId, CancellationToken token = default);

        /// <summary>
        /// Marca múltiples eventos como procesados.
        /// </summary>
        Task MarcarComoProcesadosAsync(IEnumerable<long> eventosIds, CancellationToken token = default);
    }
}
