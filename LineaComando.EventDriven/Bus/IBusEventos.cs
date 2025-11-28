namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    /// <summary>
    /// Abstracción opcional para coordinar publishers y handlers.
    /// </summary>
    public interface IBusEventos
    {
        /// <summary>
        /// Publica un evento a todos los handlers registrados.
        /// </summary>
        Task PublicarAsync<TEvento>(TEvento evento, CancellationToken token = default) where TEvento : class;

        /// <summary>
        /// Suscribe un handler para un tipo de evento.
        /// </summary>
        void Suscribir<TEvento>(Func<TEvento, CancellationToken, Task> manejador) where TEvento : class;

        /// <summary>
        /// Cancela la suscripción de un handler.
        /// </summary>
        void CancelarSuscripcion<TEvento>(Func<TEvento, CancellationToken, Task> manejador) where TEvento : class;
    }
}
