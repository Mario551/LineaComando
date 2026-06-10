using PER.Mensajeria.API.Contexto;

namespace PER.Mensajeria.Builder;

public interface IContextoMensajeriaBuilder
{
    IContextoMensajeriaBuilder AgregarFiltro<TFiltro>()
        where TFiltro : class, IFiltroContextoConversacion;

    IContextoMensajeriaBuilder UsarIntencion<TIntencion>()
        where TIntencion : class, IIntencionContextoConversacionServicio;

    IContextoMensajeriaBuilder UsarCatalogoComandos<TCatalogo>()
        where TCatalogo : class, IProveedorCatalogoComandoContextoServicio;

    IContextoMensajeriaBuilder UsarEjecutorComandos<TEjecutor>()
        where TEjecutor : class, IEjecutorComandoContextoServicio;

    IContextoMensajeriaBuilder UsarProveedorHistorial<THistorial>()
        where THistorial : class, IProveedorHistorialContextoServicio;
}
