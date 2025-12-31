namespace PER.Comandos.LineaComandos.EventDriven.DAO
{
    /// <summary>
    /// Configuración de ejecución de handlers.
    /// Define CUÁNDO y CÓMO se ejecuta cada handler.
    /// </summary>
    public class DisparadorManejador
    {
        /// <summary>
        /// Identificador único.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// FK a ManejadorEvento - qué handler.
        /// </summary>
        public int ManejadorEventoId { get; set; }

        /// <summary>
        /// Modo de disparo: "Evento" o "Programado".
        /// </summary>
        public string ModoDisparo { get; set; } = "Evento";

        /// <summary>
        /// FK a TipoEvento (si ModoDisparo = "Evento").
        /// </summary>
        public int? TipoEventoId { get; set; }

        /// <summary>
        /// Expresión cron (si ModoDisparo = "Programado").
        /// </summary>
        public string? Expresion { get; set; }

        /// <summary>
        /// Si este trigger está activo.
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Orden de ejecución (menor = primero).
        /// </summary>
        public int Prioridad { get; set; }

        /// <summary>
        /// Fecha de creación.
        /// </summary>
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegación al handler.
        /// </summary>
        public ManejadorEvento? ManejadorEvento { get; set; }

        /// <summary>
        /// Navegación al tipo de evento.
        /// </summary>
        public TipoEvento? TipoEvento { get; set; }
    }
}
