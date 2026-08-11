namespace PER.Mensajeria.API.Infobip;

public class ConfiguracionClienteInfobip
{
    public ConfiguracionClienteInfobip(Uri servidor, string apiKey)
    {
        Servidor = servidor;
        ApiKey = apiKey;
    }

    public Uri Servidor { get; }
    public string ApiKey { get; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validar()
    {
        if (!Servidor.IsAbsoluteUri
            || (Servidor.Scheme != Uri.UriSchemeHttp
                && Servidor.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "El servidor Infobip debe ser una URI HTTP o HTTPS absoluta.",
                nameof(Servidor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ApiKey);

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Timeout),
                "El timeout de Infobip debe ser mayor que cero.");
        }
    }
}
