namespace PER.Comandos.LineaComandos.Cola.Notificaciones
{
    public sealed class NotificacionEjecucionComando
    {
        public Guid EjecucionId { get; }
        public long ComandoId { get; }
        public string RutaComando { get; }
        public NotificacionEjecucionComandoTipo Tipo { get; }
        public OrigenEjecucionComandoTipo Origen { get; }
        public string? CodigoOrigen { get; }
        public long? AgregadoId { get; }
        public DateTime Fecha { get; }
        public TimeSpan? Duracion { get; }
        public string? Error { get; }

        public NotificacionEjecucionComando(
            Guid ejecucionId,
            long comandoId,
            string rutaComando,
            NotificacionEjecucionComandoTipo tipo,
            OrigenEjecucionComandoTipo origen,
            string? codigoOrigen,
            long? agregadoId,
            DateTime fecha,
            TimeSpan? duracion,
            string? error)
        {
            if (ejecucionId == Guid.Empty)
                throw new ArgumentException("El identificador de ejecución no puede estar vacío.", nameof(ejecucionId));

            if (string.IsNullOrWhiteSpace(rutaComando))
                throw new ArgumentException("La ruta del comando no puede estar vacía.", nameof(rutaComando));

            EjecucionId = ejecucionId;
            ComandoId = comandoId;
            RutaComando = rutaComando;
            Tipo = tipo;
            Origen = origen;
            CodigoOrigen = codigoOrigen;
            AgregadoId = agregadoId;
            Fecha = fecha;
            Duracion = duracion;
            Error = error;
        }
    }
}
