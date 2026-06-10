namespace PER.Mensajeria.Entidad.DTO;

public class DTOContextoConversacionSolicitud
{
    public long IDProcesamientoInternoMensaje { get; set; }
    public long IDMensaje { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public long IDCuentaCanal { get; set; }
    public string TipoMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public string? IdentificadorExternoMensaje { get; set; }
    public DateTime FechaMensaje { get; set; }
    public List<DTOArchivoMensaje> Archivos { get; set; } = [];
}
