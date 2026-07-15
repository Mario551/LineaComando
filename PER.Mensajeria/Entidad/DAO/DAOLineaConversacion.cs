namespace PER.Mensajeria.Entidad.DAO;

public class DAOLineaConversacion
{
    public long ID { get; set; }
    public long IDConversacion { get; set; }
    public long? IDEstadoContextoInicial { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaUltimaActividad { get; set; }
    public bool Activa { get; set; }
}
