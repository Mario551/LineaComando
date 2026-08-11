namespace PER.Mensajeria.Entidad.DTO;

public class DTOEnvioMensajePendiente
{
    public long IDEnvioMensaje { get; set; }
    public string Canal { get; set; } = string.Empty;
    public string Cuenta { get; set; } = string.Empty;
    public string TipoDestinatario { get; set; } = string.Empty;
    public string IdentificadorDestinatario { get; set; } = string.Empty;
    public DTOMensajeSaliente Mensaje { get; set; } = new();
}
