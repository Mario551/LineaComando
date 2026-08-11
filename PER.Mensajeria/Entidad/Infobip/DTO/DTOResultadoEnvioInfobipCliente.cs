namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOResultadoEnvioInfobipCliente
{
    public bool EsExitosoHttp { get; set; }
    public bool EsTimeout { get; set; }
    public bool EsResultadoIncierto { get; set; }
    public int? StatusHttp { get; set; }
    public string CuerpoRespuesta { get; set; } = string.Empty;
    public DTOInfobipRespuestaEnvio? Respuesta { get; set; }
    public DTOInfobipError? ErrorRespuesta { get; set; }
    public string? ErrorTecnico { get; set; }
}
