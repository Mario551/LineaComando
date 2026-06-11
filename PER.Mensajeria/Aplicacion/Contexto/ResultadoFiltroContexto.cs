namespace PER.Mensajeria.Aplicacion.Contexto;

public class ResultadoFiltroContexto
{
    public bool Continuar { get; set; } = true;
    public string? Error { get; set; }

    public static ResultadoFiltroContexto ContinuarFlujo()
    {
        return new ResultadoFiltroContexto
        {
            Continuar = true
        };
    }

    public static ResultadoFiltroContexto DetenerConError(string error)
    {
        return new ResultadoFiltroContexto
        {
            Continuar = false,
            Error = error
        };
    }
}
