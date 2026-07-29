using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;
using PER.Mensajeria.Builder.Contexto.LineaComando;

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

    public IContextoMensajeriaBuilder UsarIntencionOpenRouter(
        string apiKey,
        Action<IOpenRouterMensajeriaBuilder> configurar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(configurar);

        OpenRouterMensajeriaBuilder openRouterBuilder = new(servicios, apiKey);
        configurar(openRouterBuilder);
        if (!openRouterBuilder.ModeloConfigurado)
        {
            throw new InvalidOperationException("OpenRouter debe configurar un adapter de modelo.");
        }

        return this;
    }

    public IContextoMensajeriaBuilder UsarIntencionOpenCode(
        string promptAgente,
        string nombreAgente,
        Action<ConfiguracionIntencionOpenCode> configurar)
    {
        ArgumentNullException.ThrowIfNull(configurar);

        ConfiguracionIntencionOpenCode configuracion =
            new(promptAgente, nombreAgente);
        configurar(configuracion);
        ValidarConfiguracionOpenCode(configuracion);

        RemoverServicios<ConfiguracionIntencionOpenCode>();
        RemoverServicios<IOpenCodeAgenteAdaptador>();
        RemoverServicios<IIntencionContextoConversacionServicio>();
        RemoverServicios<IOpenCodeCliente>();

        servicios.AddSingleton(configuracion);
        servicios.AddTransient<
            IOpenCodeAgenteAdaptador,
            OpenCodeAgenteAdaptador>();
        servicios.AddTransient<
            IIntencionContextoConversacionServicio,
            OpenCodeIntencionContextoServicio>();
        servicios
            .AddHttpClient<IOpenCodeCliente, OpenCodeCliente>(cliente =>
            {
                cliente.BaseAddress = NormalizarServidor(
                    configuracion.Servidor!);
                cliente.Timeout = configuracion.Timeout;

                string credenciales = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{configuracion.AutenticacionBasica!.Usuario}:"
                        + configuracion.AutenticacionBasica.Contrasena));
                cliente.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Basic",
                        credenciales);
            })
            .RedactLoggedHeaders(["Authorization"]);

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

    public IContextoMensajeriaBuilder UsarEjecutorLineaComando()
    {
        ReemplazarTransient<IEjecutorComandoContextoServicio, EjecutorComandoLineaComandoServicio>();
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

    private static void ValidarConfiguracionOpenCode(
        ConfiguracionIntencionOpenCode configuracion)
    {
        if (configuracion.Servidor is null
            || !configuracion.Servidor.IsAbsoluteUri
            || configuracion.Servidor.Scheme
                is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "OpenCode requiere un servidor HTTP o HTTPS absoluto.");
        }

        if (configuracion.AutenticacionBasica is null)
        {
            throw new InvalidOperationException(
                "OpenCode requiere configuracion de autenticacion basica.");
        }

        if (configuracion.Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "El timeout de OpenCode debe ser mayor que cero.");
        }
    }

    private static Uri NormalizarServidor(Uri servidor)
    {
        string valor = servidor.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? servidor.AbsoluteUri
            : servidor.AbsoluteUri + "/";
        return new Uri(valor);
    }
}
