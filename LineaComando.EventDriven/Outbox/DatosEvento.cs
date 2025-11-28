namespace PER.Comandos.LineaComandos.EventDriven.Outbox
{
    /// <summary>
    /// Datos de un evento para persistir en el outbox.
    /// </summary>
    public class DatosEvento
    {
        /// <summary>
        /// Código del tipo de evento.
        /// </summary>
        public string TipoEvento { get; set; } = string.Empty;

        /// <summary>
        /// ID del agregado/entidad afectada.
        /// </summary>
        public Guid? AgregadoId { get; set; }

        /// <summary>
        /// Datos completos del evento (serializados).
        /// </summary>
        public string Datos { get; set; } = string.Empty;

        /// <summary>
        /// Metadatos adicionales (correlación, usuario, etc).
        /// </summary>
        public string? Metadatos { get; set; }
    }
}
