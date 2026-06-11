namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class ResultadoContextoConversacion
{
    public ResultadoContextoConversacionTipo TipoResultado { get; set; }
    public string? Error { get; set; }
    public List<DTOMensajeSaliente> MensajesSalientes { get; set; } = [];
}
