namespace PER.Mensajeria.Aplicacion.EnviarMensaje;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.API.Canal;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class EnviarMensajeAplicacion : IEnviarMensajeAplicacion
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ICanalMensajeAPI canalMensajeAPI;

    public EnviarMensajeAplicacion(IUnitOfWork unitOfWork, ICanalMensajeAPI canalMensajeAPI)
    {
        this.unitOfWork = unitOfWork;
        this.canalMensajeAPI = canalMensajeAPI;
    }

    public async Task<DTOResultadoEnvioMensaje> EjecutarAsync(long idEnvioMensaje, CancellationToken cancellationToken)
    {
        DAOEnvioMensaje envio = await unitOfWork.EnvioMensajeRepositorio.Get()
            .SingleAsync(envioActual => envioActual.ID == idEnvioMensaje, cancellationToken);

        if (envio.IDEstadoEnvioMensaje != "pendiente")
        {
            DTOResultadoEnvioMensaje resultadoExistente = new()
            {
                IDEnvioMensaje = envio.ID,
                Estado = envio.IDEstadoEnvioMensaje,
                Error = envio.Error
            };

            unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);

            return resultadoExistente;
        }

        DAOMensaje mensaje = await unitOfWork.MensajeRepositorio.GetNoTracking()
            .SingleAsync(mensajeActual => mensajeActual.ID == envio.IDMensaje, cancellationToken);
        DAOLineaConversacion linea = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            .SingleAsync(lineaActual => lineaActual.ID == mensaje.IDLineaConversacion, cancellationToken);
        List<DTOArchivoMensaje> archivos = await unitOfWork.ArchivoMensajeRepositorio.GetNoTracking()
            .Where(archivoActual => archivoActual.IDMensaje == mensaje.ID)
            .Select(archivoActual => new DTOArchivoMensaje
            {
                NombreArchivo = archivoActual.NombreArchivo,
                TipoContenido = archivoActual.IDTipoContenidoArchivo,
                TamanoBytes = archivoActual.TamanoBytes,
                UbicacionArchivo = archivoActual.UbicacionArchivo,
                ProveedorAlmacenamiento = archivoActual.ProveedorAlmacenamiento,
                IdentificadorExternoArchivo = archivoActual.IdentificadorExternoArchivo
            })
            .ToListAsync(cancellationToken);

        DTOMensajeSaliente mensajeSaliente = new()
        {
            IDConversacion = linea.IDConversacion,
            IDLineaConversacion = linea.ID,
            TipoMensaje = mensaje.IDTipoMensaje,
            TelefonoOrigen = mensaje.TelefonoOrigen,
            TelefonoDestino = mensaje.TelefonoDestino,
            Contenido = mensaje.Contenido,
            FechaMensaje = mensaje.FechaMensaje,
            Archivos = archivos
        };

        DTOResultadoEnvioMensaje resultado = await canalMensajeAPI.EnviarAsync(mensajeSaliente, cancellationToken);
        DateTime fecha = DateTime.Now;
        envio.Intentos++;
        envio.FechaUltimoIntento = fecha;

        if (resultado.Estado == "enviado")
        {
            envio.IDEstadoEnvioMensaje = "enviado";
            envio.Error = null;
            envio.FechaEnviado = fecha;
        }
        else
        {
            envio.IDEstadoEnvioMensaje = string.IsNullOrWhiteSpace(resultado.Estado) ? "fallido" : resultado.Estado;
            envio.Error = string.IsNullOrWhiteSpace(resultado.Error) ? "Error al enviar mensaje." : resultado.Error;
            envio.FechaEnviado = null;
        }

        unitOfWork.EnvioMensajeRepositorio.Actualizar(envio);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        DTOResultadoEnvioMensaje resultadoEnvio = new()
        {
            IDEnvioMensaje = envio.ID,
            Estado = envio.IDEstadoEnvioMensaje,
            Error = envio.Error
        };

        unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);

        return resultadoEnvio;
    }
}
