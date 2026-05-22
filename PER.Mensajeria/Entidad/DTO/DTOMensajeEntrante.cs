namespace PER.Mensajeria.Entidad.DTO;

public class DTOMensajeEntrante
{
    public string Canal { get; set; } = string.Empty;
    public string Cuenta { get; set; } = string.Empty;
    public string IdentificadorParticipante { get; set; } = string.Empty;
    public string TipoParticipante { get; set; } = string.Empty;
    public string TipoMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public string? IdentificadorExternoMensaje { get; set; }
    public DateTime FechaMensaje { get; set; }
    public List<DTOArchivoMensaje> Archivos { get; set; } = [];
}
