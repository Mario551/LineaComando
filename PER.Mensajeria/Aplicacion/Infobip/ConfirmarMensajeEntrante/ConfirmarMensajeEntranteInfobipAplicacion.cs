using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Aplicacion.Infobip.ConfirmarMensajeEntrante;

public class ConfirmarMensajeEntranteInfobipAplicacion :
    IConfirmarMensajeEntranteInfobipAplicacion
{
    private readonly IUnitOfWork unitOfWork;

    public ConfirmarMensajeEntranteInfobipAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task EjecutarAsync(
        DTORegistrarMensajeEntranteSolicitud solicitud,
        DTORegistrarMensajeEntranteRespuesta resultado,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(resultado);
        string messageId = solicitud.Mensaje.IdentificadorExternoMensaje
            ?? throw new InvalidOperationException(
                "La confirmacion Infobip requiere IdentificadorExternoMensaje.");

        long? idWebhookReceiptInfobip = await unitOfWork.WebhookReceiptInfobipRepositorio
            .GetNoTracking()
            .Where(recepcion => recepcion.MessageId == messageId)
            .Select(recepcion => (long?)recepcion.RecordId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!idWebhookReceiptInfobip.HasValue)
        {
            throw new InvalidOperationException(
                $"No existe recepcion Infobip para messageId '{messageId}'.");
        }

        DAOProcesamientoMensajeEntranteInfobip procesamiento = await unitOfWork
            .ProcesamientoMensajeEntranteInfobipRepositorio
            .Get()
            .SingleOrDefaultAsync(
                actual => actual.IDWebhookReceiptInfobip == idWebhookReceiptInfobip.Value,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"No existe procesamiento Infobip para messageId '{messageId}'.");

        if (procesamiento.IDEstado == "procesado")
        {
            if (procesamiento.IDMensaje != resultado.IDMensaje)
            {
                throw new InvalidOperationException(
                    $"La recepcion Infobip ya esta relacionada con el mensaje {procesamiento.IDMensaje}.");
            }

            unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio.LiberarRastreo(
                procesamiento);
            return;
        }

        if (procesamiento.IDEstado == "error")
        {
            throw new InvalidOperationException(
                "No se puede confirmar una recepcion Infobip en estado error.");
        }

        DAOMensaje mensaje = await unitOfWork.MensajeRepositorio.GetNoTracking()
            .SingleOrDefaultAsync(
                mensajeActual => mensajeActual.ID == resultado.IDMensaje,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"No existe el mensaje generico {resultado.IDMensaje}.");

        if (mensaje.IdentificadorExternoMensaje != messageId)
        {
            throw new InvalidOperationException(
                "El mensaje generico no corresponde a la recepcion Infobip confirmada.");
        }

        procesamiento.IDMensaje = resultado.IDMensaje;
        procesamiento.IDEstado = "procesado";
        procesamiento.Error = null;
        procesamiento.FechaProcesado = DateTime.Now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio.LiberarRastreo(
            procesamiento);
    }
}
