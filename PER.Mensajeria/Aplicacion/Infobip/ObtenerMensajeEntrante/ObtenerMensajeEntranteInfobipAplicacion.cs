using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Infobip.Mapeo;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DTO;
using PER.Mensajeria.Entidad.Infobip.DAO;

namespace PER.Mensajeria.Aplicacion.Infobip.ObtenerMensajeEntrante;

public class ObtenerMensajeEntranteInfobipAplicacion :
    IObtenerMensajeEntranteInfobipAplicacion
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IConvertidorMensajeEntranteInfobipServicio convertidor;

    public ObtenerMensajeEntranteInfobipAplicacion(
        IUnitOfWork unitOfWork,
        IConvertidorMensajeEntranteInfobipServicio convertidor)
    {
        this.unitOfWork = unitOfWork;
        this.convertidor = convertidor;
    }

    public async Task<DTORegistrarMensajeEntranteSolicitud?> EjecutarAsync(
        long idWebhookReceiptInfobip,
        CancellationToken cancellationToken)
    {
        DAOProcesamientoMensajeEntranteInfobip procesamiento = await unitOfWork
            .ProcesamientoMensajeEntranteInfobipRepositorio
            .Get()
            .SingleOrDefaultAsync(
                actual => actual.IDWebhookReceiptInfobip == idWebhookReceiptInfobip,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"La recepcion Infobip {idWebhookReceiptInfobip} no tiene procesamiento tecnico.");

        if (procesamiento.IDEstado is "procesado" or "error")
        {
            unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio.LiberarRastreo(
                procesamiento);
            return null;
        }

        if (procesamiento.IDEstado is not "pendiente" and not "despachado")
        {
            throw new InvalidOperationException(
                $"El estado Infobip '{procesamiento.IDEstado}' no se puede despachar.");
        }

        WebhookReceiptInfobip recepcion = await unitOfWork
            .WebhookReceiptInfobipRepositorio
            .ObtenerAgregadoNoTrackingAsync(idWebhookReceiptInfobip, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No existe la recepcion Infobip {idWebhookReceiptInfobip}.");
        ResultadoConversionMensajeEntranteInfobip conversion = convertidor.Convertir(recepcion);
        procesamiento.Intentos++;

        if (!conversion.Convertido || conversion.Solicitud is null)
        {
            procesamiento.IDEstado = "error";
            procesamiento.Error = conversion.Error
                ?? "No se pudo convertir el mensaje Infobip.";
            procesamiento.FechaProcesado = DateTime.Now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio.LiberarRastreo(
                procesamiento);
            return null;
        }

        procesamiento.IDEstado = "despachado";
        procesamiento.Error = null;
        procesamiento.FechaDespachado = DateTime.Now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio.LiberarRastreo(
            procesamiento);
        return conversion.Solicitud;
    }
}
