namespace PER.Mensajeria.Core.Modelo;

public class MensajeSaliente
{
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public string TipoMensaje { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public DateTime FechaMensaje { get; set; }
}
