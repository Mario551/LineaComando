namespace PER.Mensajeria.Entidad.DTO;

public class DTOResultadoEnvioMensaje
{
    public long IDEnvioMensaje { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Error { get; set; }
}
