namespace PER.Comandos.LineaComandos.Registro
{
    /// <summary>
    /// Metadata de un comando registrado.
    /// </summary>
    public class MetadatosComando
    {
        /// <summary>
        /// Identificador único.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Ruta jerárquica del comando (ej: "orden pagar").
        /// </summary>
        public string RutaComando { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del comando.
        /// </summary>
        public string? Descripcion { get; set; }

        /// <summary>
        /// Si el comando está disponible.
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Fecha de creación.
        /// </summary>
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    }
}
