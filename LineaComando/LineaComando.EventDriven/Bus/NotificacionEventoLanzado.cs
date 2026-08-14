namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    public sealed class NotificacionEventoLanzado
    {
        public long Id { get; }
        public string NombreEvento { get; }
        public long? AgregadoId { get; }
        public string DatosEvento { get; }
        public DateTime CreadoEn { get; }

        public NotificacionEventoLanzado(
            long id,
            string nombreEvento,
            long? agregadoId,
            string datosEvento,
            DateTime creadoEn)
        {
            if (string.IsNullOrWhiteSpace(nombreEvento))
                throw new ArgumentException("El nombre del evento no puede estar vacío.", nameof(nombreEvento));

            Id = id;
            NombreEvento = nombreEvento;
            AgregadoId = agregadoId;
            DatosEvento = datosEvento ?? throw new ArgumentNullException(nameof(datosEvento));
            CreadoEn = creadoEn;
        }
    }
}
