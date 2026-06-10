using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.API.Contexto;

namespace PER.Mensajeria.Builder;

public class ContextoMensajeriaBuilder : IContextoMensajeriaBuilder
{
    private readonly IServiceCollection servicios;

    public ContextoMensajeriaBuilder(IServiceCollection servicios)
    {
        this.servicios = servicios;
    }

    public IContextoMensajeriaBuilder AgregarFiltro<TFiltro>()
        where TFiltro : class, IFiltroContextoConversacion
    {
        servicios.AddTransient<IFiltroContextoConversacion, TFiltro>();
        return this;
    }

    public IContextoMensajeriaBuilder UsarIntencion<TIntencion>()
        where TIntencion : class, IIntencionContextoConversacionServicio
    {
        ReemplazarTransient<IIntencionContextoConversacionServicio, TIntencion>();
        return this;
    }

    public IContextoMensajeriaBuilder UsarCatalogoComandos<TCatalogo>()
        where TCatalogo : class, IProveedorCatalogoComandoContextoServicio
    {
        ReemplazarTransient<IProveedorCatalogoComandoContextoServicio, TCatalogo>();
        return this;
    }

    public IContextoMensajeriaBuilder UsarEjecutorComandos<TEjecutor>()
        where TEjecutor : class, IEjecutorComandoContextoServicio
    {
        ReemplazarTransient<IEjecutorComandoContextoServicio, TEjecutor>();
        return this;
    }

    public IContextoMensajeriaBuilder UsarProveedorHistorial<THistorial>()
        where THistorial : class, IProveedorHistorialContextoServicio
    {
        ReemplazarTransient<IProveedorHistorialContextoServicio, THistorial>();
        return this;
    }

    private void ReemplazarTransient<TServicio, TImplementacion>()
        where TServicio : class
        where TImplementacion : class, TServicio
    {
        RemoverServicios<TServicio>();
        servicios.AddTransient<TServicio, TImplementacion>();
    }

    private void RemoverServicios<TServicio>()
    {
        Type tipoServicio = typeof(TServicio);

        for (int indice = servicios.Count - 1; indice >= 0; indice--)
        {
            if (servicios[indice].ServiceType == tipoServicio)
            {
                servicios.RemoveAt(indice);
            }
        }
    }
}
