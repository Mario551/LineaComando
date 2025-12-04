namespace PER.Comandos.LineaComandos.EventDriven.DAO
{
    /// <summary>
    /// Entidad: Catálogo de handlers disponibles.
    /// Los handlers son comandos que se ejecutan en respuesta a eventos o de forma programada.
    /// </summary>
    public class ManejadorEvento
    {
        /// <summary>
        /// Identificador único.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Código único del handler.
        /// </summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// Nombre legible.
        /// </summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Qué hace este handler.
        /// </summary>
        public string? Descripcion { get; set; }

        /// <summary>
        /// FK al comando registrado que se ejecutará.
        /// </summary>
        public int IdComandoRegistrado { get; set; }

        /// <summary>
        /// Ruta del comando a ejecutar (ej: "notificacion email").
        /// </summary>
        public string RutaComando { get; set; } = string.Empty;

        /// <summary>
        /// Argumentos predefinidos para el comando (opcional).
        /// </summary>
        public string? ArgumentosComando { get; set; }

        /// <summary>
        /// Si el handler está disponible.
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Fecha de creación.
        /// </summary>
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Triggers asociados a este handler.
        /// </summary>
        public ICollection<DisparadorManejador> Disparadores { get; set; } = new List<DisparadorManejador>();
    }
}
