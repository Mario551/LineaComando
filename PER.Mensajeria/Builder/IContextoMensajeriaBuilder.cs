using PER.Mensajeria.Aplicacion.Contexto;

namespace PER.Mensajeria.Builder;

public interface IContextoMensajeriaBuilder
{
    IContextoMensajeriaBuilder AgregarFiltro<TFiltro>()
        where TFiltro : class, IFiltroContextoConversacion;

    IContextoMensajeriaBuilder UsarIntencion<TIntencion>()
        where TIntencion : class, IIntencionContextoConversacionServicio;

    IContextoMensajeriaBuilder UsarIntencionOpenRouter(
        string apiKey,
        Action<IOpenRouterMensajeriaBuilder> configurar);

    IContextoMensajeriaBuilder UsarCatalogoComandos<TCatalogo>()
        where TCatalogo : class, IProveedorCatalogoComandoContextoServicio;

    IContextoMensajeriaBuilder UsarEjecutorComandos<TEjecutor>()
        where TEjecutor : class, IEjecutorComandoContextoServicio;

    IContextoMensajeriaBuilder UsarEjecutorLineaComando();

}
