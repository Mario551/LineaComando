namespace PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

public class EjecucionComandoContexto
{
    public long ID { get; set; }
    public long? IDEjecucionAnterior { get; set; }
    public long IDLineaConversacion { get; set; }
    public long IDProcesamientoInternoMensaje { get; set; }
    public long IDMetadataEntradaDecisionContextoIA { get; set; }
    public long? IDMetadataEntradaResultadoContextoIA { get; set; }
    public int NumeroIntento { get; set; }
    public string ProveedorEjecucion { get; set; } = string.Empty;
    public string? IdentificadorExterno { get; set; }
    public string CodigoComando { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public string? Error { get; set; }
    public string? ToolCallID { get; set; }
}
