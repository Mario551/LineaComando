namespace PER.Mensajeria.Core.Modelo;

public class ResultadoEnvioMensaje
{
    public long IDEnvioMensaje { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Error { get; set; }
}
