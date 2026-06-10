namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class DTOContextoConversacionEstado
{
    public DTOContextoConversacionSolicitud Solicitud { get; set; } = new();
    public IReadOnlyList<DTOComandoContexto> Comandos { get; set; } = [];
    public IReadOnlyList<DTODatoIntermedioContexto> DatosIntermedios { get; set; } = [];
    public int Iteracion { get; set; }
}
