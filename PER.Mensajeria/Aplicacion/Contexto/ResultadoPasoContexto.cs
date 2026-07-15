namespace PER.Mensajeria.Aplicacion.Contexto;

internal sealed class ResultadoPasoContexto
{
    private ResultadoPasoContexto(
        ResultadoPasoContextoTipo tipo,
        ResultadoContextoConversacion? resultadoFinal)
    {
        Tipo = tipo;
        ResultadoFinal = resultadoFinal;
    }

    public ResultadoPasoContextoTipo Tipo { get; }
    public ResultadoContextoConversacion? ResultadoFinal { get; }

    public static ResultadoPasoContexto Continuar()
    {
        return new ResultadoPasoContexto(ResultadoPasoContextoTipo.Continuar, null);
    }

    public static ResultadoPasoContexto Terminar(ResultadoContextoConversacion resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return new ResultadoPasoContexto(ResultadoPasoContextoTipo.Terminar, resultado);
    }
}
