using PER.Mensajeria.Servicio.Cola;

namespace PER.Mensajeria.Servicio.Orquestador;

public interface IOrquestadorContextoServicio : IAsyncDisposable
{
    Task EncolarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken);
}
