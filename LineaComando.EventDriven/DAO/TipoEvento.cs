namespace PER.Comandos.LineaComandos.EventDriven.DAO
{
    /// <summary>
    /// Entidad: Catálogo de tipos de eventos.
    /// Tabla conceptual: EventTypes
    /// </summary>
    public class TipoEvento
    {
        /// <summary>
        /// Identificador único.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Código único del evento (ej: "OrderStatusChanged").
        /// </summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// Nombre legible.
        /// </summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Descripción detallada del evento.
        /// </summary>
        public string? Descripcion { get; set; }

        /// <summary>
        /// Si el evento está activo.
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Fecha de creación.
        /// </summary>
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Triggers asociados a este tipo de evento.
        /// </summary>
        public ICollection<DisparadorManejador> Disparadores { get; set; } = new List<DisparadorManejador>();
    }
}
