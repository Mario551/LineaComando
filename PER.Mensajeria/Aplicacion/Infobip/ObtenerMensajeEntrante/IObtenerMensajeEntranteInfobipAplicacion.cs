using PER.Mensajeria.Entidad.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.ObtenerMensajeEntrante;

public interface IObtenerMensajeEntranteInfobipAplicacion
{
    Task<DTORegistrarMensajeEntranteSolicitud?> EjecutarAsync(
        long idWebhookReceiptInfobip,
        CancellationToken cancellationToken);
}
