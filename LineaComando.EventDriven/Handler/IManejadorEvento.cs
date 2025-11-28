namespace PER.Comandos.LineaComandos.EventDriven.Handler
{
    /// <summary>
    /// Procesar un evento específico.
    /// </summary>
    public interface IManejadorEvento<TEvento> where TEvento : class
    {
        /// <summary>
        /// Procesa el evento de forma asíncrona.
        /// </summary>
        Task EjecutarAsync(TEvento evento, CancellationToken token = default);
    }
}
