namespace PER.Mensajeria.Entidad.DAO;

public class DAOCompactacionContextoConversacion
{
    public long ID { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacionOrigen { get; set; }
    public long? IDCompactacionContextoAnterior { get; set; }
    public long IDInformacionTecnicaLlamadaIA { get; set; }
    public int Version { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
}
