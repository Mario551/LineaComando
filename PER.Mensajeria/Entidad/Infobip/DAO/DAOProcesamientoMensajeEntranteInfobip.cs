namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class DAOProcesamientoMensajeEntranteInfobip
{
    public long ID { get; set; }
    public long IDWebhookReceiptInfobip { get; set; }
    public string IDEstado { get; set; } = string.Empty;
    public long? IDMensaje { get; set; }
    public int Intentos { get; set; }
    public string? Error { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaDespachado { get; set; }
    public DateTime? FechaProcesado { get; set; }

    public virtual WebhookReceiptInfobip WebhookReceiptInfobip { get; set; } = null!;
}
