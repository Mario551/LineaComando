using PER.Mensajeria.Servicio.Cola;

namespace PER.Mensajeria.Servicio.Orquestador;

public interface IOrquestadorContextoServicio
{
    Task ProcesarAsync(EventoMensajeria eventoMensajeria, CancellationToken cancellationToken);
}
