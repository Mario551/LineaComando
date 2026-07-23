namespace PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

public class EventoMensajeriaEntrada
{
    public long IDMensaje { get; set; }
    public long IDProcesamientoInternoMensaje { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
