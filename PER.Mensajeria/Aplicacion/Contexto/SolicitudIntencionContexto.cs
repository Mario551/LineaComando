namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class SolicitudIntencionContexto
{
    public SolicitudContextoConversacion Solicitud { get; set; } = new();
    public IReadOnlyList<ComandoContexto> Comandos { get; set; } = [];
    public IReadOnlyList<DatoIntermedioContexto> DatosIntermedios { get; set; } = [];
    public EstadoContextoConversacion? EstadoContextoInicial { get; set; }
    public IReadOnlyList<EntradaContextoIA> EntradasContextoIA { get; set; } = [];
    public int Iteracion { get; set; }
}
