namespace PER.Mensajeria.Core.Modelo;

public class MensajeEntrante
{
    public string Canal { get; set; } = string.Empty;
    public string Cuenta { get; set; } = string.Empty;
    public string IdentificadorParticipante { get; set; } = string.Empty;
    public string TipoMensaje { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public string? IdentificadorExternoMensaje { get; set; }
    public DateTime FechaMensaje { get; set; }
}
