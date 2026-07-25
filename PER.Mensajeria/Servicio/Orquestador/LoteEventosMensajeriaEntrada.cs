using PER.Mensajeria.Aplicacion.ColaMensajeria.Entrada;

namespace PER.Mensajeria.Servicio.Orquestador;

internal sealed class LoteEventosMensajeriaEntrada
{
    public required long IDConversacion { get; init; }
    public required long IDLineaConversacion { get; init; }
    public required IReadOnlyList<EventoMensajeriaEntrada> Eventos { get; init; }
}
