namespace PER.Mensajeria.Aplicacion.Contexto;

public class EstadoContextoConversacion
{
    public long ID { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacionOrigen { get; set; }
    public long? IDEstadoContextoAnterior { get; set; }
    public int Version { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
}
