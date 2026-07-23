namespace PER.Mensajeria.Aplicacion.Contexto;

public class MensajeSalienteContexto
{
    public string TipoMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public DateTime FechaMensaje { get; set; }
    public List<ArchivoMensajeContexto> Archivos { get; set; } = [];
}
