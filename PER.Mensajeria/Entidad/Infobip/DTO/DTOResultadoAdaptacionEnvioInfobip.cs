namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOResultadoAdaptacionEnvioInfobip
{
    public bool Exitosa { get; set; }
    public DTOInfobipSolicitudEnvio? Solicitud { get; set; }
    public string? Error { get; set; }
}
