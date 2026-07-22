namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class RegistrarMensajeSalidaAplicacion : IRegistrarMensajeSalidaAplicacion
{
    private readonly IUnitOfWorkFactory unitOfWorkFactory;

    public RegistrarMensajeSalidaAplicacion(IUnitOfWorkFactory unitOfWorkFactory)
    {
        this.unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<DTORegistrarMensajeSalidaRespuesta> EjecutarAsync(DTORegistrarMensajeSalidaSolicitud solicitud, CancellationToken cancellationToken)
    {
        await using IUnitOfWorkScope alcanceUnitOfWork = unitOfWorkFactory.Crear();
        IUnitOfWork unitOfWork = alcanceUnitOfWork.UnitOfWork;

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
            List<DAOArchivoMensaje> archivos = [];
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
                DAOArchivoMensaje archivo = new()
                {
                    IDMensaje = mensaje.ID,
                    IDTipoContenidoArchivo = archivoSolicitud.TipoContenido,
                    NombreArchivo = archivoSolicitud.NombreArchivo,
                    TamanoBytes = archivoSolicitud.TamanoBytes,
                    UbicacionArchivo = archivoSolicitud.UbicacionArchivo,
                    ProveedorAlmacenamiento = archivoSolicitud.ProveedorAlmacenamiento,
                    IdentificadorExternoArchivo = archivoSolicitud.IdentificadorExternoArchivo,
                    FechaCreacion = fecha
                };

                archivos.Add(archivo);
                await unitOfWork.ArchivoMensajeRepositorio.AgregarAsync(archivo, cancellationToken);
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

            long idMensaje = mensaje.ID;
            long idEnvioMensaje = envio.ID;

            foreach (DAOArchivoMensaje archivo in archivos)
            {
                unitOfWork.ArchivoMensajeRepositorio.LiberarRastreo(archivo);
            }

            unitOfWork.MensajeRepositorio.LiberarRastreo(mensaje);
            unitOfWork.EnvioMensajeRepositorio.LiberarRastreo(envio);
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(linea);

            return new DTORegistrarMensajeSalidaRespuesta
            {
                IDMensaje = idMensaje,
                IDEnvioMensaje = idEnvioMensaje,
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
