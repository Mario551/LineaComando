namespace PER.Mensajeria.Aplicacion.CargarEventosMensajeriaPendientes;

public class EventoMensajeriaPendiente
{
    public long IDMensaje { get; set; }
    public long IDProcesamientoInternoMensaje { get; set; }
    public string IDEstadoProcesamientoInternoMensaje { get; set; } = string.Empty;
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
