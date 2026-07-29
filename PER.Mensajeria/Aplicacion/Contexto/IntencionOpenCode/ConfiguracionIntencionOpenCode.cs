namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class ConfiguracionIntencionOpenCode
{
    public ConfiguracionIntencionOpenCode(
        string promptAgente,
        string nombreAgente)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptAgente);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreAgente);

        PromptAgente = promptAgente;
        NombreAgente = nombreAgente;
    }

    public string PromptAgente { get; }
    public string NombreAgente { get; }
    public Uri? Servidor { get; set; }
    public ConfiguracionAutenticacionBasicaOpenCode? AutenticacionBasica { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
