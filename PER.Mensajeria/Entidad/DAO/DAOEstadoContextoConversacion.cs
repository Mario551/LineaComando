namespace PER.Mensajeria.Entidad.DAO;

public class DAOEstadoContextoConversacion
{
    public long ID { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacionOrigen { get; set; }
    public long? IDEstadoContextoAnterior { get; set; }
    public long IDMetadataRazonamientoIA { get; set; }
    public int Version { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
}
