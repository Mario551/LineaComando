namespace PER.Mensajeria.Aplicacion.Contexto;

public class EstadoIteracionContextoConversacion
{
    public SolicitudContextoConversacion Solicitud { get; set; } = new();
    public IReadOnlyList<ComandoContexto> Comandos { get; set; } = [];
    public IReadOnlyList<DatoIntermedioContexto> DatosIntermedios { get; set; } = [];
    public int Iteracion { get; set; }
}
