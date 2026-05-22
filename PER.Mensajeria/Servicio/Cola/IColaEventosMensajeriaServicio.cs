namespace PER.Mensajeria.Servicio.Cola;

public interface IColaEventosMensajeriaServicio
{
    void Publicar(EventoMensajeria eventoMensajeria);
    Task<EventoMensajeria> ConsumirAsync(CancellationToken cancellationToken);
}
