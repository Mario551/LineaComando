namespace PER.Mensajeria.Entidad.DAO;

public class DAOEjecucionComandoContexto
{
    public long ID { get; set; }
    public long? IDEjecucionAnterior { get; set; }
    public long IDLineaConversacion { get; set; }
    public long IDProcesamientoInternoMensaje { get; set; }
    public long IDMetadataEntradaDecisionContextoIA { get; set; }
    public long? IDMetadataEntradaResultadoContextoIA { get; set; }
    public int NumeroIntento { get; set; }
    public string ProveedorEjecucion { get; set; } = string.Empty;
    public string? IdentificadorExterno { get; set; }
    public string CodigoComando { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = string.Empty;
    public string IDEstadoEjecucionComandoContexto { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public string? Error { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaInicioEncolado { get; set; }
    public DateTime? FechaEncolado { get; set; }
    public DateTime? FechaFinalizacion { get; set; }
}
