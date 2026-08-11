using PER.Mensajeria.Datos.Contexto;
using PER.Mensajeria.Entidad.Infobip.DAO;
using Microsoft.EntityFrameworkCore;

namespace PER.Mensajeria.Datos.Repositorio.Infobip;

public class WebhookReceiptInfobipRepositorio :
    Repositorio<WebhookReceiptInfobip>,
    IWebhookReceiptInfobipRepositorio
{
    public WebhookReceiptInfobipRepositorio(MensajeriaContextoDB contexto)
        : base(contexto)
    {
    }

    public Task<WebhookReceiptInfobip?> ObtenerAgregadoNoTrackingAsync(
        long idWebhookReceiptInfobip,
        CancellationToken cancellationToken)
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.MessageContextInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.MessageReferralInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.TextMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.LocationMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ImageMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.DocumentMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.AudioMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.VideoMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.VoiceMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ContactMessageInfobip)
                    .ThenInclude(contacto => contacto!.SharedContactsInfobip)
                        .ThenInclude(contacto => contacto.ContactAddressesInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ContactMessageInfobip)
                    .ThenInclude(contacto => contacto!.SharedContactsInfobip)
                        .ThenInclude(contacto => contacto.ContactEmailsInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ContactMessageInfobip)
                    .ThenInclude(contacto => contacto!.SharedContactsInfobip)
                        .ThenInclude(contacto => contacto.ContactPhonesInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ContactMessageInfobip)
                    .ThenInclude(contacto => contacto!.SharedContactsInfobip)
                        .ThenInclude(contacto => contacto.ContactUrlsInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.InfectedContentMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ButtonMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.StickerMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.InteractiveButtonReplyMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.InteractiveListReplyMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.FlowReplyMessageInfobip)
                    .ThenInclude(flow => flow!.FlowResponseNodesInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.PaymentConfirmationMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.CallPermissionReplyMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.InThreadAuthenticationReplyMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.OrderMessageInfobip)
                    .ThenInclude(orden => orden!.OrderProductItemsInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.ReactionMessageInfobip)
            .Include(recepcion => recepcion.InboundMessageInfobip)
                .ThenInclude(mensaje => mensaje.UnsupportedMessageInfobip)
            .SingleOrDefaultAsync(
                recepcion => recepcion.RecordId == idWebhookReceiptInfobip,
                cancellationToken);
    }
}
