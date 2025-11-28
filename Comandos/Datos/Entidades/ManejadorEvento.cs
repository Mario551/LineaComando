namespace PER.Comandos.Datos.Entidades
{
    /// <summary>
    /// Entidad: Catálogo de handlers disponibles.
    /// Tabla conceptual: EventHandlers
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
        /// Namespace completo de la clase .NET.
        /// </summary>
        public string NombreClase { get; set; } = string.Empty;

        /// <summary>
        /// Qué hace este handler.
        /// </summary>
        public string? Descripcion { get; set; }

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

        /// <summary>
        /// Ejecuciones de este handler.
        /// </summary>
        public ICollection<EjecucionManejador> Ejecuciones { get; set; } = new List<EjecucionManejador>();
    }
}
