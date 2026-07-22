namespace PER.Mensajeria.Aplicacion.Contexto;

public class MetadataEntradaContextoIA
{
    public long ID { get; set; }
    public long IDLineaConversacion { get; set; }
    public long? IDMensaje { get; set; }
    public long? IDProcesamientoInternoMensaje { get; set; }
    public long? IDInformacionTecnicaLlamadaIA { get; set; }
    public long? IDCompactacionContextoIncorporada { get; set; }
    public int Orden { get; set; }
    public string IDRolContextoIA { get; set; } = string.Empty;
    public string IDTipoEntradaContextoIA { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public string? ToolCallID { get; set; }
    public DateTime FechaEntrada { get; set; }
    public DateTime FechaCreacion { get; set; }
    public InformacionTecnicaLlamadaIAContexto? InformacionTecnicaLlamadaIA { get; set; }
}
