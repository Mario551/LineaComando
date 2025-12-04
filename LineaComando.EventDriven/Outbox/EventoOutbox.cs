namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    /// <summary>
    /// Evento almacenado en el outbox.
    /// </summary>
    public class EventoOutbox
    {
        /// <summary>
        /// Identificador único del evento.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Código del tipo de evento.
        /// </summary>
        public string CodigoTipoEvento { get; set; } = string.Empty;

        /// <summary>
        /// ID del agregado/entidad afectada.
        /// </summary>
        public long? AgregadoId { get; set; }

        /// <summary>
        /// Datos completos del evento (JSON).
        /// </summary>
        public string DatosEvento { get; set; } = string.Empty;

        /// <summary>
        /// Metadatos adicionales.
        /// </summary>
        public string? Metadatos { get; set; }

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
