namespace PER.Mensajeria.Entidad.DTO;

public class DTORegistrarMensajeEntranteRespuesta
{
    public long IDMensaje { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacion { get; set; }
    public long IDProcesamientoInternoMensaje { get; set; }
    public bool Registrado { get; set; }
}
