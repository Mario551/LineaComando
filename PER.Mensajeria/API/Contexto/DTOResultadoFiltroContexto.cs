namespace PER.Mensajeria.API.Contexto;

public class DTOResultadoFiltroContexto
{
    public bool Continuar { get; set; } = true;
    public string? Error { get; set; }

    public static DTOResultadoFiltroContexto ContinuarFlujo()
    {
        return new DTOResultadoFiltroContexto
        {
            Continuar = true
        };
    }

    public static DTOResultadoFiltroContexto DetenerConError(string error)
    {
        return new DTOResultadoFiltroContexto
        {
            Continuar = false,
            Error = error
        };
    }
}
