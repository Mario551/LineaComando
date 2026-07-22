namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public class ConfiguracionMiniMaxOpenRouter
{
    public ConfiguracionMiniMaxOpenRouter(string promptAgente)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptAgente);
        PromptAgente = promptAgente;
    }

    public string PromptAgente { get; }
    public string Modelo { get; set; } = "minimax/minimax-m3";
    public string Proveedor { get; set; } = "minimax";
    public int LimiteVentanaTokens { get; } = 1_000_000;
    public int MaximoTokens { get; set; } = 30000;
    public decimal? Temperatura { get; set; }
    public bool? RazonamientoHabilitado { get; set; }
    public string? EsfuerzoRazonamiento { get; set; }
    public int? MaximoTokensRazonamiento { get; set; }
    public bool? ExcluirRazonamiento { get; set; }
    public int MaximoLlamadasCompactacion { get; set; } = 32;
}
