namespace PER.Mensajeria.Entidad.DTO;

public class DTOResultadoContextoConversacion
{
    public DTOResultadoContextoConversacionTipo TipoResultado { get; set; }
    public string? Error { get; set; }
    public List<DTOMensajeSaliente> MensajesSalientes { get; set; } = [];
}
