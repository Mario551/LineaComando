using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Infobip.Mapeo;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.RegistrarWebhook;

public class RegistrarWebhookInfobipAplicacion : IRegistrarWebhookInfobipAplicacion
{
    private const string EstadoPendiente = "pendiente";

    private readonly IUnitOfWork unitOfWork;
    private readonly IMapeadorWebhookInfobipServicio mapeadorWebhookInfobipServicio;

    public RegistrarWebhookInfobipAplicacion(
        IUnitOfWork unitOfWork,
        IMapeadorWebhookInfobipServicio mapeadorWebhookInfobipServicio)
    {
        this.unitOfWork = unitOfWork;
        this.mapeadorWebhookInfobipServicio = mapeadorWebhookInfobipServicio;
    }

    public async Task<DTOResultadoRecepcionMensajeInfobip> EjecutarAsync(
        DTOInfobipResult resultado,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        string messageId = resultado.MessageId ?? string.Empty;
        DTOResultadoRecepcionMensajeInfobip? existente = await ObtenerExistenteAsync(
            messageId,
            cancellationToken);

        if (existente is not null)
        {
            return existente;
        }

        DateTime fechaCreacion = DateTime.Now;
        WebhookReceiptInfobip recepcion;

        try
        {
            recepcion = mapeadorWebhookInfobipServicio.Mapear(resultado, fechaCreacion);
        }
        catch (InvalidOperationException excepcion)
        {
            return new DTOResultadoRecepcionMensajeInfobip
            {
                MessageId = messageId,
                Estado = "error",
                Registrado = false,
                Error = excepcion.Message
            };
        }

        DAOProcesamientoMensajeEntranteInfobip procesamiento = new()
        {
            IDEstado = EstadoPendiente,
            Intentos = 0,
            FechaCreacion = fechaCreacion,
            WebhookReceiptInfobip = recepcion
        };
        recepcion.ProcesamientoMensajeEntranteInfobip = procesamiento;

        bool transaccionIniciada = false;

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            transaccionIniciada = true;
            await unitOfWork.WebhookReceiptInfobipRepositorio.AgregarAsync(
                recepcion,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transaccionIniciada = false;

            return new DTOResultadoRecepcionMensajeInfobip
            {
                MessageId = recepcion.MessageId,
                IDWebhookReceiptInfobip = recepcion.RecordId,
                Estado = procesamiento.IDEstado,
                Registrado = true
            };
        }
        catch (DbUpdateException)
        {
            if (transaccionIniciada)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                transaccionIniciada = false;
            }

            DTOResultadoRecepcionMensajeInfobip? duplicado = await ObtenerExistenteAsync(
                messageId,
                cancellationToken);

            if (duplicado is not null)
            {
                return duplicado;
            }

            throw;
        }
        catch
        {
            if (transaccionIniciada)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
            }

            throw;
        }
    }

    private async Task<DTOResultadoRecepcionMensajeInfobip?> ObtenerExistenteAsync(
        string messageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        return await (
            from recepcion in unitOfWork.WebhookReceiptInfobipRepositorio.GetNoTracking()
            join procesamiento in unitOfWork.ProcesamientoMensajeEntranteInfobipRepositorio.GetNoTracking()
                on recepcion.RecordId equals procesamiento.IDWebhookReceiptInfobip
            where recepcion.MessageId == messageId
            select new DTOResultadoRecepcionMensajeInfobip
            {
                MessageId = recepcion.MessageId,
                IDWebhookReceiptInfobip = recepcion.RecordId,
                Estado = procesamiento.IDEstado,
                Registrado = false,
                Error = procesamiento.Error
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
