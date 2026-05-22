namespace PER.Mensajeria.Entidad.DAO;

public class DAOConversacionParticipante
{
    public long ID { get; set; }
    public long IDConversacion { get; set; }
    public long IDParticipanteConversacion { get; set; }
    public DateTime FechaUnion { get; set; }
    public DateTime? FechaSalida { get; set; }
    public bool Activo { get; set; }
}
