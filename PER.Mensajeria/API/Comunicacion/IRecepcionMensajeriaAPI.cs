namespace PER.Mensajeria.API.Comunicacion;

using PER.Mensajeria.Entidad.DTO;

public interface IRecepcionMensajeriaAPI
{
    Task<DTORegistrarMensajeEntranteSolicitud> EsperarMensajeEntranteAsync(
        CancellationToken cancellationToken);
}
