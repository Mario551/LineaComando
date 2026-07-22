namespace PER.Mensajeria.Aplicacion.Contexto;

public class InformacionTecnicaLlamadaIAContexto
{
    public string Proveedor { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Adaptador { get; set; } = string.Empty;
    public int Iteracion { get; set; }
    public string AccionDecidida { get; set; } = string.Empty;
    public string? FinishReason { get; set; }
    public string? NativeFinishReason { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? ReasoningTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? Content { get; set; }
    public string? Reasoning { get; set; }
    public string? ReasoningDetailsJson { get; set; }
    public string? Error { get; set; }
}
