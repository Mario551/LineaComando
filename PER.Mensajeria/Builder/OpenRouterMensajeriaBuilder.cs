using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using PER.Mensajeria.Aplicacion.Contexto;
using PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

namespace PER.Mensajeria.Builder;

public class OpenRouterMensajeriaBuilder : IOpenRouterMensajeriaBuilder
{
    private static readonly Uri Endpoint = new("https://openrouter.ai/api/v1/");
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly IServiceCollection servicios;
    private readonly string apiKey;

    public OpenRouterMensajeriaBuilder(IServiceCollection servicios, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        this.servicios = servicios;
        this.apiKey = apiKey;
    }

    public bool ModeloConfigurado { get; private set; }

    public IOpenRouterMensajeriaBuilder UsarMiniMax(
        string promptAgente,
        Action<ConfiguracionMiniMaxOpenRouter>? configurar = null)
    {
        ConfiguracionMiniMaxOpenRouter configuracion = new(promptAgente);
        configurar?.Invoke(configuracion);

        RemoverServicios<ConfiguracionMiniMaxOpenRouter>();
        RemoverServicios<IOpenRouterModeloAdaptador>();
        RemoverServicios<IIntencionContextoConversacionServicio>();
        RemoverServicios<IOpenRouterCliente>();

        servicios.AddSingleton(configuracion);
        servicios.AddTransient<IOpenRouterModeloAdaptador, MiniMaxOpenRouterAdaptador>();
        servicios.AddTransient<IIntencionContextoConversacionServicio, OpenRouterIntencionContextoServicio>();
        servicios
            .AddHttpClient<IOpenRouterCliente, OpenRouterCliente>(cliente =>
            {
                cliente.BaseAddress = Endpoint;
                cliente.Timeout = Timeout;
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            })
            .RedactLoggedHeaders(["Authorization"]);

        ModeloConfigurado = true;
        return this;
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
