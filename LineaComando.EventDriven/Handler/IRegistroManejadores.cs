namespace PER.Comandos.LineaComandos.EventDriven.Handler
{
    /// <summary>
    /// Responsabilidad: Leer configuración de handlers.
    /// </summary>
    public interface IRegistroManejadores
    {
        /// <summary>
        /// Obtiene los handlers configurados para un tipo de evento.
        /// </summary>
        Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresParaEventoAsync(string tipoEvento, CancellationToken token = default);

        /// <summary>
        /// Obtiene los handlers programados (scheduled).
        /// </summary>
        Task<IEnumerable<ConfiguracionManejador>> ObtenerManejadoresProgramadosAsync(CancellationToken token = default);

        /// <summary>
        /// Actualiza la configuración de un handler.
        /// </summary>
        Task ActualizarConfiguracionAsync(ConfiguracionManejador configuracion, CancellationToken token = default);
    }
}
