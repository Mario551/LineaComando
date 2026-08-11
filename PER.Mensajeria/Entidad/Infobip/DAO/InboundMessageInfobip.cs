using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class InboundMessageInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdWebhookReceiptsInfobip { get; set; }
    public required string Type { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual WebhookReceiptInfobip WebhookReceiptInfobip { get; set; } = null!;
    public virtual MessageTypeInfobip MessageTypeInfobip { get; set; } = null!;
    public virtual MessageContextInfobip? MessageContextInfobip { get; set; }
    public virtual MessageReferralInfobip? MessageReferralInfobip { get; set; }
    public virtual TextMessageInfobip? TextMessageInfobip { get; set; }
    public virtual LocationMessageInfobip? LocationMessageInfobip { get; set; }
    public virtual ImageMessageInfobip? ImageMessageInfobip { get; set; }
    public virtual DocumentMessageInfobip? DocumentMessageInfobip { get; set; }
    public virtual AudioMessageInfobip? AudioMessageInfobip { get; set; }
    public virtual VideoMessageInfobip? VideoMessageInfobip { get; set; }
    public virtual VoiceMessageInfobip? VoiceMessageInfobip { get; set; }
    public virtual ContactMessageInfobip? ContactMessageInfobip { get; set; }
    public virtual InfectedContentMessageInfobip? InfectedContentMessageInfobip { get; set; }
    public virtual ButtonMessageInfobip? ButtonMessageInfobip { get; set; }
    public virtual StickerMessageInfobip? StickerMessageInfobip { get; set; }
    public virtual InteractiveButtonReplyMessageInfobip? InteractiveButtonReplyMessageInfobip { get; set; }
    public virtual InteractiveListReplyMessageInfobip? InteractiveListReplyMessageInfobip { get; set; }
    public virtual FlowReplyMessageInfobip? FlowReplyMessageInfobip { get; set; }
    public virtual PaymentConfirmationMessageInfobip? PaymentConfirmationMessageInfobip { get; set; }
    public virtual CallPermissionReplyMessageInfobip? CallPermissionReplyMessageInfobip { get; set; }
    public virtual InThreadAuthenticationReplyMessageInfobip? InThreadAuthenticationReplyMessageInfobip { get; set; }
    public virtual OrderMessageInfobip? OrderMessageInfobip { get; set; }
    public virtual ReactionMessageInfobip? ReactionMessageInfobip { get; set; }
    public virtual UnsupportedMessageInfobip? UnsupportedMessageInfobip { get; set; }
}
