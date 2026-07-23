namespace PER.Mensajeria.Aplicacion.RegistrarMensajeSalida;

public class ArchivoRegistrarMensajeSalida
{
    public string? NombreArchivo { get; set; }
    public string TipoContenido { get; set; } = string.Empty;
    public long? TamanoBytes { get; set; }
    public string UbicacionArchivo { get; set; } = string.Empty;
    public string ProveedorAlmacenamiento { get; set; } = string.Empty;
    public string? IdentificadorExternoArchivo { get; set; }
}
