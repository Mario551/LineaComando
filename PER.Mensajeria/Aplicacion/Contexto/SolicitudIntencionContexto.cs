namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class SolicitudIntencionContexto
{
    public SolicitudContextoConversacion Solicitud { get; set; } = new();
    public IReadOnlyList<ComandoContexto> Comandos { get; set; } = [];
    public IReadOnlyList<DatoIntermedioContexto> DatosIntermedios { get; set; } = [];
    public CompactacionContextoConversacion? CompactacionContextoInicial { get; set; }
    public IReadOnlyList<MetadataEntradaContextoIA> MetadataEntradasContextoIA { get; set; } = [];
    public int Iteracion { get; set; }
}
