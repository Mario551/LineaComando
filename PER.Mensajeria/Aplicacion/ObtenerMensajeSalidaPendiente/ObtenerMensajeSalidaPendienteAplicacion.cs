namespace PER.Mensajeria.Aplicacion.ObtenerMensajeSalidaPendiente;

using Microsoft.EntityFrameworkCore;
using PER.Mensajeria.Datos.UnitOfWork;
using PER.Mensajeria.Entidad.DTO;

public class ObtenerMensajeSalidaPendienteAplicacion : IObtenerMensajeSalidaPendienteAplicacion
{
    private const string EstadoPendiente = "pendiente";

    private readonly IUnitOfWork unitOfWork;

    public ObtenerMensajeSalidaPendienteAplicacion(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async Task<DTOEnvioMensajePendiente?> EjecutarAsync(
        long idEnvioMensaje,
        CancellationToken cancellationToken)
    {
        IQueryable<DatosEnvioPendiente> consulta =
            from envio in unitOfWork.EnvioMensajeRepositorio.GetNoTracking()
            join mensaje in unitOfWork.MensajeRepositorio.GetNoTracking()
                on envio.IDMensaje equals mensaje.ID
            join linea in unitOfWork.LineaConversacionRepositorio.GetNoTracking()
                on mensaje.IDLineaConversacion equals linea.ID
            join conversacion in unitOfWork.ConversacionRepositorio.GetNoTracking()
                on linea.IDConversacion equals conversacion.ID
            join cuenta in unitOfWork.CuentaCanalRepositorio.GetNoTracking()
                on conversacion.IDCuentaCanal equals cuenta.ID
            join canal in unitOfWork.CanalComunicacionRepositorio.GetNoTracking()
                on cuenta.IDCanalComunicacion equals canal.ID
            where envio.ID == idEnvioMensaje
                && envio.IDEstadoEnvioMensaje == EstadoPendiente
            select new DatosEnvioPendiente
            {
                IDEnvioMensaje = envio.ID,
                IDMensaje = mensaje.ID,
                IDConversacion = conversacion.ID,
                IDLineaConversacion = linea.ID,
                Canal = canal.Canal,
                Cuenta = cuenta.Cuenta,
                TipoMensaje = mensaje.IDTipoMensaje,
                TelefonoOrigen = mensaje.TelefonoOrigen,
                TelefonoDestino = mensaje.TelefonoDestino,
                Contenido = mensaje.Contenido,
                FechaMensaje = mensaje.FechaMensaje
            };

        DatosEnvioPendiente? datos = await consulta.SingleOrDefaultAsync(cancellationToken);
        if (datos is null)
        {
            return null;
        }

        List<DTOArchivoMensaje> archivos = await unitOfWork.ArchivoMensajeRepositorio.GetNoTracking()
            .Where(archivo => archivo.IDMensaje == datos.IDMensaje)
            .OrderBy(archivo => archivo.ID)
            .Select(archivo => new DTOArchivoMensaje
            {
                NombreArchivo = archivo.NombreArchivo,
                TipoContenido = archivo.IDTipoContenidoArchivo,
                TamanoBytes = archivo.TamanoBytes,
                UbicacionArchivo = archivo.UbicacionArchivo,
                ProveedorAlmacenamiento = archivo.ProveedorAlmacenamiento,
                IdentificadorExternoArchivo = archivo.IdentificadorExternoArchivo
            })
            .ToListAsync(cancellationToken);

        return new DTOEnvioMensajePendiente
        {
            IDEnvioMensaje = datos.IDEnvioMensaje,
            Canal = datos.Canal,
            Cuenta = datos.Cuenta,
            Mensaje = new DTOMensajeSaliente
            {
                IDConversacion = datos.IDConversacion,
                IDLineaConversacion = datos.IDLineaConversacion,
                TipoMensaje = datos.TipoMensaje,
                TelefonoOrigen = datos.TelefonoOrigen,
                TelefonoDestino = datos.TelefonoDestino,
                Contenido = datos.Contenido,
                FechaMensaje = datos.FechaMensaje,
                Archivos = archivos
            }
        };
    }

    private sealed class DatosEnvioPendiente
    {
        public long IDEnvioMensaje { get; init; }
        public long IDMensaje { get; init; }
        public long IDConversacion { get; init; }
        public long IDLineaConversacion { get; init; }
        public required string Canal { get; init; }
        public required string Cuenta { get; init; }
        public required string TipoMensaje { get; init; }
        public string? TelefonoOrigen { get; init; }
        public string? TelefonoDestino { get; init; }
        public string? Contenido { get; init; }
        public DateTime FechaMensaje { get; init; }
    }
}
