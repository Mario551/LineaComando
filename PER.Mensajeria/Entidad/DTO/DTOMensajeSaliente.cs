namespace PER.Mensajeria.Entidad.DTO;

public class DTOMensajeSaliente
{
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public string TipoMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public DateTime FechaMensaje { get; set; }
    public List<DTOArchivoMensaje> Archivos { get; set; } = [];
}
