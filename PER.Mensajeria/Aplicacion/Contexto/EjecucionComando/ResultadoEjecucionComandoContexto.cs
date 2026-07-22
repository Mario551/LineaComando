namespace PER.Mensajeria.Aplicacion.Contexto.EjecucionComando;

public class ResultadoEjecucionComandoContexto
{
    public ResultadoComandoContexto Resultado { get; set; } = ResultadoComandoContexto.Fallo("La ejecucion no tiene resultado.");
    public MetadataEntradaContextoIA? MetadataEntradaResultado { get; set; }
}
