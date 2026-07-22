using PER.Mensajeria.Servicio.Cola;

namespace ServicioTest.Fakes;

public class FakeColaEventosMensajeriaServicio : IColaEventosMensajeriaServicio
{
    public EventoMensajeria? EventoPublicado { get; private set; }

    public void Publicar(EventoMensajeria eventoMensajeria)
    {
        EventoPublicado = eventoMensajeria;
    }

    public void PublicarRehidratado(EventoMensajeria eventoMensajeria)
    {
        EventoPublicado = eventoMensajeria;
    }

    public Task<EventoMensajeria> ConsumirAsync(CancellationToken cancellationToken)
    {
        if (EventoPublicado is null)
        {
            throw new InvalidOperationException("No hay evento publicado.");
        }

        return Task.FromResult(EventoPublicado);
    }
}
