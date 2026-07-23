namespace PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

public interface IColaEventosMensajeriaEntradaServicio
{
    void Publicar(EventoMensajeriaEntrada eventoMensajeria);
    void PublicarRehidratado(EventoMensajeriaEntrada eventoMensajeria);
    Task<EventoMensajeriaEntrada> ConsumirAsync(CancellationToken cancellationToken);
}
