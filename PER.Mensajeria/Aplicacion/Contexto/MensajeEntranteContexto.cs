namespace PER.Mensajeria.Aplicacion.Contexto;

public sealed class MensajeEntranteContexto
{
    public long IDProcesamientoInternoMensaje { get; set; }
    public long IDMensaje { get; set; }
    public string TipoMensaje { get; set; } = string.Empty;
    public string? TelefonoOrigen { get; set; }
    public string? TelefonoDestino { get; set; }
    public string? Contenido { get; set; }
    public string? IdentificadorExternoMensaje { get; set; }
    public DateTime FechaMensaje { get; set; }
    public List<ArchivoMensajeContexto> Archivos { get; set; } = [];
}
