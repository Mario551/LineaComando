namespace PER.Mensajeria.Servicio.Cola;

public interface IColaEventosMensajeriaServicio
{
    void Publicar(EventoMensajeria eventoMensajeria);
    void PublicarRehidratado(EventoMensajeria eventoMensajeria);
    Task<EventoMensajeria> ConsumirAsync(CancellationToken cancellationToken);
}
