using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.Infobip.DAO;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.Aplicacion.Infobip.Envio;

public class RegistrarIntentoEnvioInfobipAplicacion :
    IRegistrarIntentoEnvioInfobipAplicacion
{
    private const string EstadoPendiente = "pendiente";
    private const string EstadoEnviando = "enviando";
    private const string EstadoFallido = "fallido";
    private const string EstadoIncierto = "incierto";

    private readonly IUnitOfWork unitOfWork;

    public RegistrarIntentoEnvioInfobipAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task<long> IniciarAsync(
        long idEnvioMensaje,
        DTOInfobipSolicitudEnvio solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.CuerpoJson);

        DAOIntentoEnvioMensajeInfobip intento = await RegistrarAsync(
            idEnvioMensaje,
            EstadoEnviando,
            solicitud.Endpoint,
            solicitud.CuerpoJson,
            null,
            cancellationToken);
        return intento.ID;
    }

    public async Task RegistrarFalloAdaptacionAsync(
        long idEnvioMensaje,
        string error,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        await RegistrarAsync(
            idEnvioMensaje,
            EstadoFallido,
            null,
            null,
            error,
            cancellationToken);
    }

    public async Task FinalizarAsync(
        long idIntento,
        string estado,
        DTOResultadoEnvioInfobipCliente resultado,
        string? error,
        CancellationToken cancellationToken)
    {
        if (estado is not "aceptado" and not EstadoFallido and not EstadoIncierto)
        {
            throw new ArgumentException(
                $"El estado de intento Infobip '{estado}' no es válido.",
                nameof(estado));
        }

        ArgumentNullException.ThrowIfNull(resultado);
        DAOIntentoEnvioMensajeInfobip intento = await unitOfWork
            .IntentoEnvioMensajeInfobipRepositorio
            .Get()
            .SingleAsync(intentoActual => intentoActual.ID == idIntento, cancellationToken);

        if (intento.IDEstado != EstadoEnviando)
        {
            unitOfWork.IntentoEnvioMensajeInfobipRepositorio.LiberarRastreo(intento);
            return;
        }

        DTOInfobipEstadoEnvio? estadoInfobip = resultado.Respuesta?.Status;
        intento.IDEstado = estado;
        intento.RespuestaJson = resultado.CuerpoRespuesta;
        intento.StatusHttp = resultado.StatusHttp;
        intento.MessageIDInfobip = resultado.Respuesta?.MessageId;
        intento.IDGrupoEstadoInfobip = estadoInfobip?.GroupId;
        intento.GrupoEstadoInfobip = estadoInfobip?.GroupName;
        intento.IDEstadoInfobip = estadoInfobip?.Id;
        intento.EstadoInfobip = estadoInfobip?.Name;
        intento.DescripcionEstadoInfobip = estadoInfobip?.Description;
        intento.Error = error;
        intento.FechaFinalizacion = DateTime.Now;

        unitOfWork.IntentoEnvioMensajeInfobipRepositorio.Actualizar(intento);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.IntentoEnvioMensajeInfobipRepositorio.LiberarRastreo(intento);
    }

    public async Task MarcarInciertoAsync(
        long idIntento,
        string error,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        DTOResultadoEnvioInfobipCliente resultado = new()
        {
            EsResultadoIncierto = true,
            ErrorTecnico = error
        };
        await FinalizarAsync(
            idIntento,
            EstadoIncierto,
            resultado,
            error,
            cancellationToken);
    }

    private async Task<DAOIntentoEnvioMensajeInfobip> RegistrarAsync(
        long idEnvioMensaje,
        string estado,
        string? endpoint,
        string? solicitudJson,
        string? error,
        CancellationToken cancellationToken)
    {
        if (idEnvioMensaje <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(idEnvioMensaje),
                "El identificador del envío debe ser mayor que cero.");
        }

        DAOEnvioMensaje envio = await unitOfWork.EnvioMensajeRepositorio.Get()
            .SingleAsync(envioActual => envioActual.ID == idEnvioMensaje, cancellationToken);
        if (envio.IDEstadoEnvioMensaje != EstadoPendiente)
        {
            unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);
            throw new InvalidOperationException(
                $"El envío {idEnvioMensaje} no está pendiente.");
        }

        bool transaccionIniciada = false;

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            transaccionIniciada = true;
            DateTime fecha = DateTime.Now;
            List<DAOIntentoEnvioMensajeInfobip> intentosAbiertos = await unitOfWork
                .IntentoEnvioMensajeInfobipRepositorio
                .Get()
                .Where(intento => intento.IDEnvioMensaje == idEnvioMensaje
                    && intento.IDEstado == EstadoEnviando)
                .ToListAsync(cancellationToken);

            foreach (DAOIntentoEnvioMensajeInfobip intentoAbierto in intentosAbiertos)
            {
                intentoAbierto.IDEstado = EstadoIncierto;
                intentoAbierto.Error =
                    "El proceso inició otro intento sin encontrar el resultado definitivo del anterior.";
                intentoAbierto.FechaFinalizacion = fecha;
                unitOfWork.IntentoEnvioMensajeInfobipRepositorio.Actualizar(intentoAbierto);
            }

            int ultimoNumero = await unitOfWork.IntentoEnvioMensajeInfobipRepositorio
                .GetNoTracking()
                .Where(intento => intento.IDEnvioMensaje == idEnvioMensaje)
                .OrderByDescending(intento => intento.NumeroIntento)
                .Select(intento => (int?)intento.NumeroIntento)
                .FirstOrDefaultAsync(cancellationToken)
                ?? 0;

            DAOIntentoEnvioMensajeInfobip intento = new()
            {
                IDEnvioMensaje = idEnvioMensaje,
                NumeroIntento = ultimoNumero + 1,
                IDEstado = estado,
                Endpoint = endpoint,
                SolicitudJson = solicitudJson,
                Error = error,
                FechaInicio = fecha,
                FechaFinalizacion = estado == EstadoEnviando ? null : fecha
            };

            await unitOfWork.IntentoEnvioMensajeInfobipRepositorio.AgregarAsync(
                intento,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transaccionIniciada = false;

            foreach (DAOIntentoEnvioMensajeInfobip intentoAbierto in intentosAbiertos)
            {
                unitOfWork.IntentoEnvioMensajeInfobipRepositorio.LiberarRastreo(intentoAbierto);
            }

            unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);
            unitOfWork.IntentoEnvioMensajeInfobipRepositorio.LiberarRastreo(intento);
            return intento;
        }
        catch
        {
            if (transaccionIniciada)
            {
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
