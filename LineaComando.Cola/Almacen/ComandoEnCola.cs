namespace PER.Comandos.LineaComandos.Cola.Almacen
{
    /// <summary>
    /// Comando encolado para procesamiento asíncrono.
    /// </summary>
    public class ComandoEnCola
    {
        /// <summary>
        /// Identificador único del comando encolado.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Ruta del comando (ej: "orden pagar").
        /// </summary>
        public string RutaComando { get; set; } = string.Empty;

        /// <summary>
        /// Argumentos del comando (ej: "--orderId=123 --monto=100").
        /// </summary>
        public string Argumentos { get; set; } = string.Empty;

        /// <summary>
        /// Datos adicionales en JSON.
        /// </summary>
        public string? DatosDeComando { get; set; }

        /// <summary>
        /// Cuándo se encoló el comando.
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cuándo se procesó (NULL = pendiente).
        /// </summary>
        public DateTime? FechaEjecucion { get; set; }

        /// <summary>
        /// Estado del procesamiento.
        /// </summary>
        public string Estado { get; set; } = "Pendiente";

        /// <summary>
        /// Mensaje de error si falló.
        /// </summary>
        public string? MensajeError { get; set; }

        /// <summary>
        /// Número de intentos de ejecución.
        /// </summary>
        public int Intentos { get; set; }
    }
}
