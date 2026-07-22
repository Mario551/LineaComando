namespace PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

public class ResultadoRegistrarDecisionContextoIA
{
    public MetadataEntradaContextoIA MetadataEntradaDecision { get; set; } = new();
    public EjecucionComandoContexto? EjecucionComando { get; set; }
}
