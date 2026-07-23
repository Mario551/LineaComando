namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

public class SolicitudRegistrarMensajeSalida
{
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public string TipoMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public DateTime FechaMensaje { get; set; }
    public List<ArchivoRegistrarMensajeSalida> Archivos { get; set; } = [];
}
