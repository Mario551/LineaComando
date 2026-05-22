using PER.Mensajeria.Servicio.Cola;

namespace PER.Mensajeria.Servicio.Orquestador;

public class OrquestadorContextoServicio : IOrquestadorContextoServicio
{
    public Task ProcesarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
