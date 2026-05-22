namespace PER.Mensajeria.Entidad.DAO;

public class DAOMensaje
{
    public long ID { get; set; }
    public long IDLineaConversacion { get; set; }
    public string IDTipoMensaje { get; set; } = string.Empty;
    public string IDDireccionMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public string? IdentificadorExternoMensaje { get; set; }
    public DateTime FechaMensaje { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
