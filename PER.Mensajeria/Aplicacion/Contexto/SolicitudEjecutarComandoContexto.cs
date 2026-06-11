namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class SolicitudEjecutarComandoContexto
{
    public SolicitudContextoConversacion Solicitud { get; set; } = new();
    public ComandoContexto Comando { get; set; } = new();
    public IReadOnlyDictionary<string, string> Parametros { get; set; } = new Dictionary<string, string>();
}
