namespace PER.Mensajeria.API.Comunicacion;

using PER.Mensajeria.Entidad.DTO;

public interface IComunicacionMensajeriaAPI
{
    Task<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
        CancellationToken cancellationToken);

    Task<DTOResultadoEnvioMensaje> EnviarMensajeAsync(
        DTOEnvioMensajePendiente mensaje,
        CancellationToken cancellationToken);
}
