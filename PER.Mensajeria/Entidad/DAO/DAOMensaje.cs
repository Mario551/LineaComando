namespace PER.Mensajeria.Entidad.DAO;

public class DAOMensaje
{
    private DateTime fechaMensaje;

    public long ID { get; set; }
    public long IDLineaConversacion { get; set; }
    public string IDTipoMensaje { get; set; } = string.Empty;
    public string IDDireccionMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public string? IdentificadorExternoMensaje { get; set; }
    public DateTime FechaMensaje
    {
        get => fechaMensaje;
        set => fechaMensaje = NormalizarPrecisionPostgreSql(value);
    }

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }

    private static DateTime NormalizarPrecisionPostgreSql(DateTime fecha)
    {
        long ticks = fecha.Ticks - fecha.Ticks % 10;
        return new DateTime(ticks, fecha.Kind);
    }
}
