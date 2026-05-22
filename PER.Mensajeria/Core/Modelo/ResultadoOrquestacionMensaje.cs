namespace PER.Mensajeria.Core.Modelo;

public class ResultadoOrquestacionMensaje
{
    public long IDProcesamientoInternoMensaje { get; set; }
    public bool Procesado { get; set; }
    public string? Error { get; set; }
}
