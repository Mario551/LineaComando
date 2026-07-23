using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

namespace ServicioTest.Fakes;

public class FakeColaEventosMensajeriaEntradaServicio : IColaEventosMensajeriaEntradaServicio
{
    public EventoMensajeriaEntrada? EventoPublicado { get; private set; }

    public void Publicar(EventoMensajeriaEntrada eventoMensajeria)
    {
        EventoPublicado = eventoMensajeria;
    }

    public void PublicarRehidratado(EventoMensajeriaEntrada eventoMensajeria)
    {
        EventoPublicado = eventoMensajeria;
    }

    public Task<EventoMensajeriaEntrada> ConsumirAsync(CancellationToken cancellationToken)
    {
        if (EventoPublicado is null)
        {
            throw new InvalidOperationException("No hay evento publicado.");
        }

        return Task.FromResult(EventoPublicado);
    }
}
