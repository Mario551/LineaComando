namespace PER.Mensajeria.Entidad.DAO;

public class DAOEnvioMensaje
{
    public long ID { get; set; }
    public long IDMensaje { get; set; }
    public string IDEstadoEnvioMensaje { get; set; } = string.Empty;
    public int Intentos { get; set; }
    public string? Error { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaUltimoIntento { get; set; }
    public DateTime? FechaEnviado { get; set; }
}
