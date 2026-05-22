namespace PER.Mensajeria.Entidad.DAO;

public class DAOArchivoMensaje
{
    public long ID { get; set; }
    public long IDMensaje { get; set; }
    public string IDTipoContenidoArchivo { get; set; } = string.Empty;
    public string? NombreArchivo { get; set; }
    public long? TamanoBytes { get; set; }
    public string UbicacionArchivo { get; set; } = string.Empty;
    public string ProveedorAlmacenamiento { get; set; } = string.Empty;
    public string? IdentificadorExternoArchivo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
