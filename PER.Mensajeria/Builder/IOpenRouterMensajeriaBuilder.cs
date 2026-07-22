using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

namespace PER.Mensajeria.Builder;

public interface IOpenRouterMensajeriaBuilder
{
    IOpenRouterMensajeriaBuilder UsarMiniMax(
        string promptAgente,
        Action<ConfiguracionMiniMaxOpenRouter>? configurar = null);
}
