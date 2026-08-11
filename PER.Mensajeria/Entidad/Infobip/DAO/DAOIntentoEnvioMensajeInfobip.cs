namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class DAOIntentoEnvioMensajeInfobip
{
    public long ID { get; set; }
    public long IDEnvioMensaje { get; set; }
    public int NumeroIntento { get; set; }
    public string IDEstado { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? SolicitudJson { get; set; }
    public string? RespuestaJson { get; set; }
    public int? StatusHttp { get; set; }
    public string? MessageIDInfobip { get; set; }
    public int? IDGrupoEstadoInfobip { get; set; }
    public string? GrupoEstadoInfobip { get; set; }
    public int? IDEstadoInfobip { get; set; }
    public string? EstadoInfobip { get; set; }
    public string? DescripcionEstadoInfobip { get; set; }
    public string? Error { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFinalizacion { get; set; }
}
