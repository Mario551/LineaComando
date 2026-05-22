namespace PER.Mensajeria.Core.Modelo;

public class ResultadoRegistroMensaje
{
    public long IDMensaje { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public bool Registrado { get; set; }
}
