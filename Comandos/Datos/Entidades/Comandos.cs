namespace PER.Comandos.Datos.Entidades
{
    /// <summary>
    /// Entidad: Comando registrado en el sistema.
    /// Tabla conceptual: RegisteredCommands
    /// </summary>
    public class ComandoRegistrado
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
        /// Esquema de parámetros esperados (JSONB).
        /// </summary>
        public string? EsquemaParametros { get; set; }

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
