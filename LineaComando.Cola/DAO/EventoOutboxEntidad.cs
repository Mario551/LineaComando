namespace PER.Comandos.LineaComandos.Cola.DAO
{
    /// <summary>
    /// Entidad: Event Store (Outbox).
    /// Tabla conceptual: OutboxEvents
    /// Almacena todos los eventos que han ocurrido.
    /// </summary>
    public class EventoOutboxEntidad
    {
        /// <summary>
        /// Identificador único del evento.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Código del tipo de evento.
        /// </summary>
        public string CodigoTipoEvento { get; set; } = string.Empty;

        /// <summary>
        /// ID de la entidad afectada.
        /// </summary>
        public Guid? AgregadoId { get; set; }

        /// <summary>
        /// Datos completos del evento (JSONB).
        /// </summary>
        public string DatosEvento { get; set; } = string.Empty;

        /// <summary>
        /// Cuándo ocurrió el evento.
        /// </summary>
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cuándo se terminó de procesar (NULL = pendiente).
        /// </summary>
        public DateTime? ProcesadoEn { get; set; }
    }
}
