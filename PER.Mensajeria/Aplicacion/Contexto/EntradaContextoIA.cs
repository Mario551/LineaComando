namespace PER.Mensajeria.Aplicacion.Contexto;

public class EntradaContextoIA
{
    public long ID { get; set; }
    public long IDLineaConversacion { get; set; }
    public long? IDMensaje { get; set; }
    public long? IDProcesamientoInternoMensaje { get; set; }
    public long? IDMetadataRazonamientoIA { get; set; }
    public int Orden { get; set; }
    public string IDRolContextoIA { get; set; } = string.Empty;
    public string IDTipoEntradaContextoIA { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public string? ToolCallID { get; set; }
    public DateTime FechaEntrada { get; set; }
    public MetadataRazonamientoIAContexto? Metadata { get; set; }
}
