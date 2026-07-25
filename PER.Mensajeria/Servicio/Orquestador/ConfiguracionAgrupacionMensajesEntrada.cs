namespace PER.Mensajeria.Servicio.Orquestador;

public sealed class ConfiguracionAgrupacionMensajesEntrada
{
    public TimeSpan TiempoInactividad { get; set; } = TimeSpan.FromSeconds(2);
    public int CantidadMaximaMensajesPorLote { get; set; } = 10;

    public void Validar()
    {
        if (TiempoInactividad <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TiempoInactividad),
                TiempoInactividad,
                "El tiempo de inactividad debe ser mayor que cero.");
        }

        if (CantidadMaximaMensajesPorLote <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CantidadMaximaMensajesPorLote),
                CantidadMaximaMensajesPorLote,
                "La cantidad maxima de mensajes por lote debe ser mayor que cero.");
        }
    }
}
