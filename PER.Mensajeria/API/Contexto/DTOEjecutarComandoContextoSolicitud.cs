namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class DTOEjecutarComandoContextoSolicitud
{
    public DTOContextoConversacionSolicitud Solicitud { get; set; } = new();
    public DTOComandoContexto Comando { get; set; } = new();
    public IReadOnlyDictionary<string, string> Parametros { get; set; } = new Dictionary<string, string>();
}
