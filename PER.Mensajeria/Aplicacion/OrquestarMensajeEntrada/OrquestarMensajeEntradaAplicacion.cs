namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class OrquestarMensajeEntradaAplicacion : IOrquestarMensajeEntradaAplicacion
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IContextoConversacionServicio contextoConversacionServicio;
    private readonly IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion;

    public OrquestarMensajeEntradaAplicacion(
        IUnitOfWork unitOfWork,
        IContextoConversacionServicio contextoConversacionServicio,
        IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion)
    {
        this.unitOfWork = unitOfWork;
        this.contextoConversacionServicio = contextoConversacionServicio;
        this.registrarMensajeSalidaAplicacion = registrarMensajeSalidaAplicacion;
    }

    public async Task EjecutarAsync(long idProcesamientoInternoMensaje, CancellationToken cancellationToken)
    {
        DAOProcesamientoInternoMensaje procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .SingleAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje, cancellationToken);
        DAOMensaje mensajeEntrada = await unitOfWork.MensajeRepositorio.GetNoTracking()
            .SingleAsync(mensajeActual => mensajeActual.ID == procesamiento.IDMensaje, cancellationToken);
        DAOLineaConversacion linea = await unitOfWork.LineaConversacionRepositorio.GetNoTracking()
            .SingleAsync(lineaActual => lineaActual.ID == mensajeEntrada.IDLineaConversacion, cancellationToken);
        DAOConversacion conversacion = await unitOfWork.ConversacionRepositorio.GetNoTracking()
            .SingleAsync(conversacionActual => conversacionActual.ID == linea.IDConversacion, cancellationToken);

        try
        {
            procesamiento.IDEstadoProcesamientoInternoMensaje = "en_proceso";
            procesamiento.Error = null;
            unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            SolicitudContextoConversacion solicitudContexto = await CrearSolicitudContextoAsync(
                procesamiento,
                mensajeEntrada,
                linea,
                conversacion,
                cancellationToken);
            ResultadoContextoConversacion resultadoContexto = await contextoConversacionServicio.ResolverAsync(
                solicitudContexto,
                cancellationToken);

            if (resultadoContexto.TipoResultado == ResultadoContextoConversacionTipo.Error)
            {
                throw new InvalidOperationException(resultadoContexto.Error ?? "El contexto devolvio error.");
            }

            foreach (DTOMensajeSaliente mensajeSaliente in resultadoContexto.MensajesSalientes)
            {
                ForzarRelacionSalida(mensajeSaliente, conversacion, linea);

                await registrarMensajeSalidaAplicacion.EjecutarAsync(new DTORegistrarMensajeSalidaSolicitud
                {
                    Mensaje = mensajeSaliente
                }, cancellationToken);
            }

            procesamiento.IDEstadoProcesamientoInternoMensaje = "procesado";
            procesamiento.FechaProcesado = DateTime.Now;
            procesamiento.Error = null;
            unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception excepcion)
        {
            procesamiento.IDEstadoProcesamientoInternoMensaje = "error";
            procesamiento.Intentos++;
            procesamiento.Error = excepcion.Message;
            unitOfWork.ProcesamientoInternoMensajeRepositorio.Actualizar(procesamiento);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ForzarRelacionSalida(
        DTOMensajeSaliente mensajeSaliente,
        DAOConversacion conversacion,
        DAOLineaConversacion linea)
    {
        mensajeSaliente.IDConversacion = conversacion.ID;
        mensajeSaliente.IDLineaConversacion = linea.ID;
    }

    private async Task<SolicitudContextoConversacion> CrearSolicitudContextoAsync(
        DAOProcesamientoInternoMensaje procesamiento,
        DAOMensaje mensajeEntrada,
        DAOLineaConversacion linea,
        DAOConversacion conversacion,
        CancellationToken cancellationToken)
    {
        List<DTOArchivoMensaje> archivos = await unitOfWork.ArchivoMensajeRepositorio.GetNoTracking()
            .Where(archivoActual => archivoActual.IDMensaje == mensajeEntrada.ID)
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

        return new SolicitudContextoConversacion
        {
            IDProcesamientoInternoMensaje = procesamiento.ID,
            IDMensaje = mensajeEntrada.ID,
            IDConversacion = conversacion.ID,
            IDLineaConversacion = linea.ID,
            IDCuentaCanal = conversacion.IDCuentaCanal,
            TipoMensaje = mensajeEntrada.IDTipoMensaje,
            TelefonoOrigen = mensajeEntrada.TelefonoOrigen,
            TelefonoDestino = mensajeEntrada.TelefonoDestino,
            Contenido = mensajeEntrada.Contenido,
            IdentificadorExternoMensaje = mensajeEntrada.IdentificadorExternoMensaje,
            FechaMensaje = mensajeEntrada.FechaMensaje,
            Archivos = archivos
        };
    }
}
