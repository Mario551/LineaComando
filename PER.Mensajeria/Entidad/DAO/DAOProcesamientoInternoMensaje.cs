namespace PER.Mensajeria.Entidad.DAO;

public class DAOProcesamientoInternoMensaje
{
    public long ID { get; set; }
    public long IDMensaje { get; set; }
    public string IDTipoProcesamientoInternoMensaje { get; set; } = string.Empty;
    public string IDEstadoProcesamientoInternoMensaje { get; set; } = string.Empty;
    public int Intentos { get; set; }
    public string? Error { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaProcesado { get; set; }
}
