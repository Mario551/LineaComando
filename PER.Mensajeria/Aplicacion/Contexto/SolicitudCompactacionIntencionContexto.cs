namespace PER.Mensajeria.Aplicacion.Contexto;

public class SolicitudCompactacionIntencionContexto
{
    public SolicitudContextoConversacion Solicitud { get; set; } = new();
    public EstadoContextoConversacion? EstadoContextoInicial { get; set; }
    public IReadOnlyList<EntradaContextoIA> EntradasContextoIA { get; set; } = [];
    public int Iteracion { get; set; }
}
