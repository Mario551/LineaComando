namespace PER.Mensajeria.Aplicacion.Contexto;

public class SolicitudCompactacionIntencionContexto
{
    public SolicitudContextoConversacion Solicitud { get; set; } = new();
    public CompactacionContextoConversacion? CompactacionContextoInicial { get; set; }
    public IReadOnlyList<MetadataEntradaContextoIA> MetadataEntradasContextoIA { get; set; } = [];
    public int Iteracion { get; set; }
}
