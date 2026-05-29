namespace PER.Mensajeria.Aplicacion.OrquestarMensajeEntrada;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DAO;
using PER.Mensajeria.Entidad.DTO;

public class OrquestarMensajeEntradaAplicacion : IOrquestarMensajeEntradaAplicacion
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion;

    public OrquestarMensajeEntradaAplicacion(
        IUnitOfWork unitOfWork,
        IRegistrarMensajeSalidaAplicacion registrarMensajeSalidaAplicacion)
    {
        this.unitOfWork = unitOfWork;
        this.registrarMensajeSalidaAplicacion = registrarMensajeSalidaAplicacion;
    }

    public async Task EjecutarAsync(long idProcesamientoInternoMensaje, CancellationToken cancellationToken)
    {
        DAOProcesamientoInternoMensaje procesamiento = await unitOfWork.ProcesamientoInternoMensajeRepositorio.Get()
            .SingleAsync(procesamientoActual => procesamientoActual.ID == idProcesamientoInternoMensaje, cancellationToken);
        DAOMensaje mensajeEntrada = await unitOfWork.MensajeRepositorio.Get()
            .SingleAsync(mensajeActual => mensajeActual.ID == procesamiento.IDMensaje, cancellationToken);
        DAOLineaConversacion linea = await unitOfWork.LineaConversacionRepositorio.Get()
            .SingleAsync(lineaActual => lineaActual.ID == mensajeEntrada.IDLineaConversacion, cancellationToken);

        try
        {
            DTORegistrarMensajeSalidaSolicitud solicitudSalida = new()
            {
                Mensaje = new DTOMensajeSaliente
                {
                    IDConversacion = linea.IDConversacion,
                    IDLineaConversacion = linea.ID,
                    TipoMensaje = mensajeEntrada.IDTipoMensaje,
                    TelefonoOrigen = mensajeEntrada.TelefonoDestino,
                    TelefonoDestino = mensajeEntrada.TelefonoOrigen,
                    Contenido = mensajeEntrada.Contenido,
                    FechaMensaje = DateTime.Now
                }
            };

            DTORegistrarMensajeSalidaRespuesta respuestaSalida = await registrarMensajeSalidaAplicacion.EjecutarAsync(solicitudSalida, cancellationToken);

            if (respuestaSalida.Registrado)
            {
                await AsegurarSalidaPersistidaAsync(mensajeEntrada, linea, cancellationToken);
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

    private async Task AsegurarSalidaPersistidaAsync(
        DAOMensaje mensajeEntrada,
        DAOLineaConversacion linea,
        CancellationToken cancellationToken)
    {
        bool salidaExiste = await unitOfWork.MensajeRepositorio.Get().AnyAsync(
            mensajeActual => mensajeActual.IDLineaConversacion == linea.ID && mensajeActual.IDDireccionMensaje == "salida",
            cancellationToken);

        if (salidaExiste)
        {
            return;
        }

        DateTime fecha = DateTime.Now;
        DAOMensaje mensajeSalida = new()
        {
            IDLineaConversacion = linea.ID,
            IDTipoMensaje = mensajeEntrada.IDTipoMensaje,
            IDDireccionMensaje = "salida",
            TelefonoOrigen = mensajeEntrada.TelefonoDestino,
            TelefonoDestino = mensajeEntrada.TelefonoOrigen,
            Contenido = mensajeEntrada.Contenido,
            FechaMensaje = fecha,
            FechaCreacion = fecha,
            FechaActualizacion = fecha
        };

        await unitOfWork.MensajeRepositorio.AgregarAsync(mensajeSalida, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await unitOfWork.EnvioMensajeRepositorio.AgregarAsync(new DAOEnvioMensaje
        {
            IDMensaje = mensajeSalida.ID,
            IDEstadoEnvioMensaje = "pendiente",
            Intentos = 0,
            FechaCreacion = fecha
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
