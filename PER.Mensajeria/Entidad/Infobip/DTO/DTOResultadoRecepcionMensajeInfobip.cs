namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOResultadoRecepcionMensajeInfobip
{
    public string MessageId { get; set; } = string.Empty;
    public long IDWebhookReceiptInfobip { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Registrado { get; set; }
    public string? Error { get; set; }
}
