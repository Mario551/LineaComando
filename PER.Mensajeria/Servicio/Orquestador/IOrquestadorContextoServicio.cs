using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

namespace PER.Mensajeria.Servicio.Orquestador;

public interface IOrquestadorContextoServicio : IAsyncDisposable
{
    Task EncolarAsync(EventoMensajeriaEntrada eventoMensajeria, CancellationToken cancellationToken);
}
