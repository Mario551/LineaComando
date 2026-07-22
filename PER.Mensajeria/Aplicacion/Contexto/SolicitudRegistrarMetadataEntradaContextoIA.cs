namespace PER.Mensajeria.Aplicacion.Contexto;

public class SolicitudRegistrarMetadataEntradaContextoIA
{
    public long IDLineaConversacion { get; set; }
    public long? IDMensaje { get; set; }
    public long? IDProcesamientoInternoMensaje { get; set; }
    public long? IDInformacionTecnicaLlamadaIA { get; set; }
    public string IDRolContextoIA { get; set; } = string.Empty;
    public string IDTipoEntradaContextoIA { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public string? ToolCallID { get; set; }
    public DateTime FechaEntrada { get; set; }
}
