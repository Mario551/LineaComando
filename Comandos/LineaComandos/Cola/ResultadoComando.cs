namespace PER.Comandos.LineaComandos.Cola
{
    /// <summary>
    /// Resultado de la ejecución de un comando.
    /// </summary>
    public class ResultadoComando
    {
        /// <summary>
        /// Si el comando se ejecutó exitosamente.
        /// </summary>
        public bool Exitoso { get; set; }

        /// <summary>
        /// Mensaje de error si falló.
        /// </summary>
        public string? MensajeError { get; set; }

        /// <summary>
        /// Datos de salida del comando.
        /// </summary>
        public string? Salida { get; set; }

        /// <summary>
        /// Duración de la ejecución.
        /// </summary>
        public TimeSpan Duracion { get; set; }

        /// <summary>
        /// Crea un resultado exitoso.
        /// </summary>
        public static ResultadoComando Exito(string? salida = null, TimeSpan duracion = default)
        {
            return new ResultadoComando
            {
                Exitoso = true,
                Salida = salida,
                Duracion = duracion
            };
        }

        /// <summary>
        /// Crea un resultado fallido.
        /// </summary>
        public static ResultadoComando Fallo(string mensajeError, TimeSpan duracion = default)
        {
            return new ResultadoComando
            {
                Exitoso = false,
                MensajeError = mensajeError,
                Duracion = duracion
            };
        }
    }
}
