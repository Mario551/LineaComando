namespace PER.Mensajeria.API.Contexto;

public class DTOResultadoComandoContexto
{
    public bool Exitoso { get; set; }
    public string? Resultado { get; set; }
    public string? Error { get; set; }

    public static DTOResultadoComandoContexto Exito(string resultado)
    {
        return new DTOResultadoComandoContexto
        {
            Exitoso = true,
            Resultado = resultado
        };
    }

    public static DTOResultadoComandoContexto Fallo(string error)
    {
        return new DTOResultadoComandoContexto
        {
            Exitoso = false,
            Error = error
        };
    }
}
