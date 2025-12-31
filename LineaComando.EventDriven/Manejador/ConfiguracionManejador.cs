namespace PER.Comandos.LineaComandos.EventDriven.Manejador
{
    /// <summary>
    /// Modelo de configuración de un handler.
    /// Define cuándo y cómo se ejecuta cada handler.
    /// Los handlers son comandos que se encolan para su ejecución.
    /// </summary>
    public class ConfiguracionManejador
    {
        /// <summary>
        /// Identificador único de la configuración.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID del handler a ejecutar.
        /// </summary>
        public int IDManejador { get; set; }

        /// <summary>
        /// ID del comando registrado que se ejecutará.
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
        /// Modo de disparo: "Evento" o "Programado".
        /// </summary>
        public string ModoDisparo { get; set; } = "Evento";

        /// <summary>
        /// Código del tipo de evento (si ModoDisparo = "Evento").
        /// </summary>
        public string? CodigoTipoEvento { get; set; }

        /// <summary>
        /// Expresión cron (si ModoDisparo = "Programado").
        /// </summary>
        public string? Expresion { get; set; }

        /// <summary>
        /// Si esta configuración está activa.
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Orden de ejecución (menor = primero).
        /// </summary>
        public int Prioridad { get; set; }

        /// <summary>
        /// Fecha de creación.
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
