using PER.Mensajeria.Aplicacion.Contexto;

namespace PER.Mensajeria.Aplicacion.RenovarLineaContexto;

public class SolicitudRenovarLineaContexto
{
    public long IDProcesamientoInternoMensaje { get; set; }
    public long IDMensaje { get; set; }
    public long IDConversacion { get; set; }
    public long IDLineaConversacionOrigen { get; set; }
    public ResultadoCompactacionIntencionContexto Compactacion { get; set; } = null!;
}
