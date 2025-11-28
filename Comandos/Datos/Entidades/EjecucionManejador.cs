namespace PER.Comandos.Datos.Entidades
{
    /// <summary>
    /// Entidad: Auditoría de ejecuciones de handlers.
    /// Tabla conceptual: HandlerExecutions
    /// Registra cada ejecución de un handler.
    /// </summary>
    public class EjecucionManejador
    {
        /// <summary>
        /// Identificador único.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// FK a EventoOutboxEntidad.
        /// </summary>
        public Guid EventoOutboxId { get; set; }

        /// <summary>
        /// FK a ManejadorEvento.
        /// </summary>
        public int ManejadorEventoId { get; set; }

        /// <summary>
        /// Estado: "Ejecutando", "Exitoso", "Fallido".
        /// </summary>
        public string Estado { get; set; } = "Ejecutando";

        /// <summary>
        /// Cuándo empezó.
        /// </summary>
        public DateTime IniciadoEn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cuándo terminó.
        /// </summary>
        public DateTime? CompletadoEn { get; set; }

        /// <summary>
        /// Error si falló.
        /// </summary>
        public string? MensajeError { get; set; }

        /// <summary>
        /// Intentos de retry.
        /// </summary>
        public int ContadorReintentos { get; set; }

        /// <summary>
        /// Navegación al evento.
        /// </summary>
        public EventoOutboxEntidad? EventoOutbox { get; set; }

        /// <summary>
        /// Navegación al handler.
        /// </summary>
        public ManejadorEvento? ManejadorEvento { get; set; }
    }
}
