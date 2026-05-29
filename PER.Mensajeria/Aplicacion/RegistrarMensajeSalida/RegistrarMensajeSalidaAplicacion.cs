namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class RegistrarMensajeSalidaAplicacion : IRegistrarMensajeSalidaAplicacion
{
    private readonly IUnitOfWork unitOfWork;

    public RegistrarMensajeSalidaAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task<DTORegistrarMensajeSalidaRespuesta> EjecutarAsync(DTORegistrarMensajeSalidaSolicitud solicitud, CancellationToken cancellationToken)
    {
        DTOMensajeSaliente mensajeSolicitud = solicitud.Mensaje;
        DAOLineaConversacion linea = await unitOfWork.LineaConversacionRepositorio.Get()
            .SingleAsync(lineaActual => lineaActual.ID == mensajeSolicitud.IDLineaConversacion, cancellationToken);

        if (linea.IDConversacion != mensajeSolicitud.IDConversacion)
        {
            throw new InvalidOperationException("La linea no pertenece a la conversacion indicada.");
        }

        bool transaccionIniciada = false;

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            transaccionIniciada = true;

            DateTime fecha = DateTime.Now;
            DAOMensaje mensaje = new()
            {
                IDLineaConversacion = mensajeSolicitud.IDLineaConversacion,
                IDTipoMensaje = mensajeSolicitud.TipoMensaje,
                IDDireccionMensaje = "salida",
                TelefonoOrigen = mensajeSolicitud.TelefonoOrigen,
                TelefonoDestino = mensajeSolicitud.TelefonoDestino,
                Contenido = mensajeSolicitud.Contenido,
                FechaMensaje = ObtenerFechaMensaje(mensajeSolicitud.FechaMensaje, fecha),
                FechaCreacion = fecha,
                FechaActualizacion = fecha
            };

            await unitOfWork.MensajeRepositorio.AgregarAsync(mensaje, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (DTOArchivoMensaje archivoSolicitud in mensajeSolicitud.Archivos)
            {
                await unitOfWork.ArchivoMensajeRepositorio.AgregarAsync(new DAOArchivoMensaje
                {
                    IDMensaje = mensaje.ID,
                    IDTipoContenidoArchivo = archivoSolicitud.TipoContenido,
                    NombreArchivo = archivoSolicitud.NombreArchivo,
                    TamanoBytes = archivoSolicitud.TamanoBytes,
                    UbicacionArchivo = archivoSolicitud.UbicacionArchivo,
                    ProveedorAlmacenamiento = archivoSolicitud.ProveedorAlmacenamiento,
                    IdentificadorExternoArchivo = archivoSolicitud.IdentificadorExternoArchivo,
                    FechaCreacion = fecha
                }, cancellationToken);
            }

            DAOEnvioMensaje envio = new()
            {
                IDMensaje = mensaje.ID,
                IDEstadoEnvioMensaje = "pendiente",
                Intentos = 0,
                FechaCreacion = fecha
            };

            await unitOfWork.EnvioMensajeRepositorio.AgregarAsync(envio, cancellationToken);
            linea.FechaUltimaActividad = fecha;
            unitOfWork.LineaConversacionRepositorio.Actualizar(linea);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transaccionIniciada = false;

            return new DTORegistrarMensajeSalidaRespuesta
            {
                IDMensaje = mensaje.ID,
                IDEnvioMensaje = envio.ID,
                Registrado = true
            };
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

    private static DateTime ObtenerFechaMensaje(DateTime fechaMensaje, DateTime fechaReferencia)
    {
        if (fechaMensaje == default)
        {
            return fechaReferencia;
        }

        return fechaMensaje;
    }
}
