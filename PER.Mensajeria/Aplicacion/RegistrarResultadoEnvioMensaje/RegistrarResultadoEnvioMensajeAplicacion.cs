namespace PER.Mensajeria.Aplicacion.RegistrarResultadoEnvioMensaje;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class RegistrarResultadoEnvioMensajeAplicacion : IRegistrarResultadoEnvioMensajeAplicacion
{
    private const string EstadoPendiente = "pendiente";
    private const string EstadoEnviado = "enviado";
    private const string EstadoFallido = "fallido";

    private readonly IUnitOfWork unitOfWork;

    public RegistrarResultadoEnvioMensajeAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task EjecutarAsync(
        DTOResultadoEnvioMensaje resultado,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        if (resultado.IDEnvioMensaje <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultado),
                "El identificador del envio debe ser mayor que cero.");
        }

        if (resultado.Estado is not EstadoEnviado and not EstadoFallido)
        {
            throw new InvalidOperationException(
                $"El estado de envio '{resultado.Estado}' no es soportado.");
        }

        DAOEnvioMensaje envio = await unitOfWork.EnvioMensajeRepositorio.Get()
            .SingleAsync(envioActual => envioActual.ID == resultado.IDEnvioMensaje, cancellationToken);

        if (envio.IDEstadoEnvioMensaje != EstadoPendiente)
        {
            unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);
            return;
        }

        DateTime fecha = DateTime.Now;
        envio.Intentos++;
        envio.FechaUltimoIntento = fecha;
        envio.IDEstadoEnvioMensaje = resultado.Estado;

        if (resultado.Estado == EstadoEnviado)
        {
            envio.Error = null;
            envio.FechaEnviado = fecha;
        }
        else
        {
            envio.Error = string.IsNullOrWhiteSpace(resultado.Error)
                ? "Error al enviar mensaje."
                : resultado.Error;
            envio.FechaEnviado = null;
        }

        unitOfWork.EnvioMensajeRepositorio.Actualizar(envio);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);
    }
}
