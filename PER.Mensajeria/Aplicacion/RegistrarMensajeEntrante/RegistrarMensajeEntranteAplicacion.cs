namespace PER.Mensajeria.Aplicacion.RegistrarMensajeEntrante;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class RegistrarMensajeEntranteAplicacion : IRegistrarMensajeEntranteAplicacion
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ConfiguracionLineaConversacion configuracionLineaConversacion;

    public RegistrarMensajeEntranteAplicacion(
        IUnitOfWork unitOfWork,
        ConfiguracionLineaConversacion configuracionLineaConversacion)
    {
        this.unitOfWork = unitOfWork;
        this.configuracionLineaConversacion = configuracionLineaConversacion;
    }

    public async Task<DTORegistrarMensajeEntranteRespuesta> EjecutarAsync(DTORegistrarMensajeEntranteSolicitud solicitud, CancellationToken cancellationToken)
    {
        DTOMensajeEntrante mensajeSolicitud = solicitud.Mensaje;
        DAOCuentaCanal cuentaCanal = await ObtenerCuentaCanalAsync(mensajeSolicitud, cancellationToken);
        DTORegistrarMensajeEntranteRespuesta? respuestaExistente = await EvitarDuplicadosMensajesEntrantesAsync(
            cuentaCanal.ID,
            mensajeSolicitud.IdentificadorExternoMensaje,
            cancellationToken);

        if (respuestaExistente is not null)
            return respuestaExistente;

        bool transaccionIniciada = false;

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            transaccionIniciada = true;

            DateTime fecha = DateTime.Now;
            DAOParticipanteConversacion participante = await ObtenerOCrearParticipanteAsync(mensajeSolicitud, cancellationToken);
            DAOConversacion conversacion = await ObtenerOCrearConversacionAsync(cuentaCanal.ID, participante.ID, fecha, cancellationToken);
            DAOLineaConversacion linea = await ObtenerOCrearLineaAsync(conversacion.ID, fecha, cancellationToken);
            List<DAOArchivoMensaje> archivos = [];

            DAOMensaje mensaje = new()
            {
                IDLineaConversacion = linea.ID,
                IDTipoMensaje = mensajeSolicitud.TipoMensaje,
                IDDireccionMensaje = "entrada",
                TelefonoOrigen = mensajeSolicitud.TelefonoOrigen,
                TelefonoDestino = mensajeSolicitud.TelefonoDestino,
                Contenido = mensajeSolicitud.Contenido,
                IdentificadorExternoMensaje = mensajeSolicitud.IdentificadorExternoMensaje,
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

            DAOProcesamientoInternoMensaje procesamiento = new()
            {
                IDMensaje = mensaje.ID,
                IDTipoProcesamientoInternoMensaje = "orquestar_entrada",
                IDEstadoProcesamientoInternoMensaje = "pendiente",
                Intentos = 0,
                FechaCreacion = fecha
            };

            await unitOfWork.ProcesamientoInternoMensajeRepositorio.AgregarAsync(procesamiento, cancellationToken);
            linea.FechaUltimaActividad = fecha;
            conversacion.FechaActualizacion = fecha;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transaccionIniciada = false;

            long idMensaje = mensaje.ID;
            long idConversacion = conversacion.ID;
            long idLineaConversacion = linea.ID;
            long idProcesamientoInternoMensaje = procesamiento.ID;

            foreach (DAOArchivoMensaje archivo in archivos)
            {
                unitOfWork.ArchivoMensajeRepositorio.LiberarRastreo(archivo);
            }

            unitOfWork.MensajeRepositorio.LiberarRastreo(mensaje);
            unitOfWork.ProcesamientoInternoMensajeRepositorio.LiberarRastreo(procesamiento);
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(linea);
            unitOfWork.ConversacionRepositorio.LiberarRastreo(conversacion);

            return new DTORegistrarMensajeEntranteRespuesta
            {
                IDMensaje = idMensaje,
                IDConversacion = idConversacion,
                IDLineaConversacion = idLineaConversacion,
                IDProcesamientoInternoMensaje = idProcesamientoInternoMensaje,
                Registrado = true
            };
        }
        catch
        {
            if (transaccionIniciada)
                await unitOfWork.RollbackTransactionAsync(cancellationToken);

            throw;
        }
    }

    private async Task<DAOCuentaCanal> ObtenerCuentaCanalAsync(
        DTOMensajeEntrante mensajeSolicitud,
        CancellationToken cancellationToken)
    {
        DAOCuentaCanal? cuentaCanal = await (
            from cuenta in unitOfWork.CuentaCanalRepositorio.GetNoTracking()
            join canal in unitOfWork.CanalComunicacionRepositorio.GetNoTracking() on cuenta.IDCanalComunicacion equals canal.ID
            where canal.Canal == mensajeSolicitud.Canal
                && cuenta.Cuenta == mensajeSolicitud.Cuenta
                && cuenta.Activa
            select cuenta)
            .SingleOrDefaultAsync(cancellationToken);

        if (cuentaCanal is null)
        {
            throw new InvalidOperationException("No existe una cuenta activa para el canal indicado.");
        }

        return cuentaCanal;
    }

    private async Task<DTORegistrarMensajeEntranteRespuesta?> EvitarDuplicadosMensajesEntrantesAsync(
        long idCuentaCanal,
        string? identificadorExternoMensaje,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identificadorExternoMensaje))
        {
            return null;
        }

        var mensajeExistente = await (
            from mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking() on mensaje.IDLineaConversacion equals linea.ID
            join conversacion in unitOfWork.ConversacionRepositorio.GetNoTracking() on linea.IDConversacion equals conversacion.ID
            where conversacion.IDCuentaCanal == idCuentaCanal
                && mensaje.IDDireccionMensaje == "entrada"
                && mensaje.IdentificadorExternoMensaje == identificadorExternoMensaje
            select new
            {
                Mensaje = mensaje,
                Linea = linea,
                Conversacion = conversacion
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (mensajeExistente is null)
        {
            return null;
        }

        DAOProcesamientoInternoMensaje procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.GetNoTracking()
            .SingleAsync(
                procesamientoActual => procesamientoActual.IDMensaje == mensajeExistente.Mensaje.ID
                    && procesamientoActual.IDTipoProcesamientoInternoMensaje == "orquestar_entrada",
                cancellationToken);

        return new DTORegistrarMensajeEntranteRespuesta
        {
            IDMensaje = mensajeExistente.Mensaje.ID,
            IDConversacion = mensajeExistente.Conversacion.ID,
            IDLineaConversacion = mensajeExistente.Linea.ID,
            IDProcesamientoInternoMensaje = procesamiento.ID,
            Registrado = false
        };
    }

    private async Task<DAOParticipanteConversacion> ObtenerOCrearParticipanteAsync(
        DTOMensajeEntrante mensajeSolicitud,
        CancellationToken cancellationToken)
    {
        DAOParticipanteConversacion? participante = await unitOfWork.ParticipanteConversacionRepositorio.GetNoTracking()
            .SingleOrDefaultAsync(
                participanteActual => participanteActual.IDTipoParticipanteConversacion == mensajeSolicitud.TipoParticipante
                    && participanteActual.IdentificadorParticipante == mensajeSolicitud.IdentificadorParticipante,
                cancellationToken);

        if (participante is not null)
        {
            return participante;
        }

        participante = new DAOParticipanteConversacion
        {
            IDTipoParticipanteConversacion = mensajeSolicitud.TipoParticipante,
            IdentificadorParticipante = mensajeSolicitud.IdentificadorParticipante
        };

        await unitOfWork.ParticipanteConversacionRepositorio.AgregarAsync(participante, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.ParticipanteConversacionRepositorio.LiberarRastreo(participante);

        return participante;
    }

    private async Task<DAOConversacion> ObtenerOCrearConversacionAsync(
        long idCuentaCanal,
        long idParticipanteConversacion,
        DateTime fecha,
        CancellationToken cancellationToken)
    {
        DAOConversacion? conversacion = await (
            from conversacionActual in unitOfWork.ConversacionRepositorio.Get()
            join conversacionParticipante in unitOfWork.ConversacionParticipanteRepositorio.Get()
                on conversacionActual.ID equals conversacionParticipante.IDConversacion
            where conversacionActual.IDCuentaCanal == idCuentaCanal
                && conversacionParticipante.IDParticipanteConversacion == idParticipanteConversacion
                && conversacionParticipante.Activo
            orderby conversacionActual.FechaActualizacion descending
            select conversacionActual)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversacion is not null)
            return conversacion;

        conversacion = new DAOConversacion
        {
            IDCuentaCanal = idCuentaCanal,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };

        await unitOfWork.ConversacionRepositorio.AgregarAsync(conversacion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        DAOConversacionParticipante nuevaConversacionParticipante = new()
        {
            IDConversacion = conversacion.ID,
            IDParticipanteConversacion = idParticipanteConversacion,
            FechaUnion = fecha,
            Activo = true
        };

        await unitOfWork.ConversacionParticipanteRepositorio.AgregarAsync(nuevaConversacionParticipante, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.ConversacionParticipanteRepositorio.LiberarRastreo(nuevaConversacionParticipante);

        return conversacion;
    }

    private async Task<DAOLineaConversacion> ObtenerOCrearLineaAsync(
        long idConversacion,
        DateTime fecha,
        CancellationToken cancellationToken)
    {
        DAOLineaConversacion? linea = await unitOfWork.LineaConversacionRepositorio.Get()
            .Where(lineaActual => lineaActual.IDConversacion == idConversacion && lineaActual.Activa)
            .OrderByDescending(lineaActual => lineaActual.FechaUltimaActividad)
            .FirstOrDefaultAsync(cancellationToken);

        if (linea is not null && fecha - linea.FechaUltimaActividad <= configuracionLineaConversacion.TiempoMaximoInactividad)
            return linea;

        if (linea is not null)
        {
            linea.Activa = false;
            unitOfWork.LineaConversacionRepositorio.Actualizar(linea);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            unitOfWork.LineaConversacionRepositorio.LiberarRastreo(linea);
        }

        linea = new DAOLineaConversacion
        {
            IDConversacion = idConversacion,
            FechaInicio = fecha,
            FechaUltimaActividad = fecha,
            Activa = true
        };

        await unitOfWork.LineaConversacionRepositorio.AgregarAsync(linea, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return linea;
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
