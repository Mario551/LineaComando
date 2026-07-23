namespace PER.Mensajeria.Aplicacion.Contexto;

public class ResultadoContextoConversacion
{
    public ResultadoContextoConversacionTipo TipoResultado { get; set; }
    public string? Error { get; set; }
    public List<MensajeSalienteContexto> MensajesSalientes { get; set; } = [];
    public ResultadoCompactacionIntencionContexto? Compactacion { get; set; }
}
