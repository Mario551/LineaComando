namespace PER.Mensajeria.Aplicacion.Contexto;

public class ResultadoComandoContexto
{
    public bool Exitoso { get; set; }
    public string? Resultado { get; set; }
    public string? Error { get; set; }

    public static ResultadoComandoContexto Exito(string resultado)
    {
        return new ResultadoComandoContexto
        {
            Exitoso = true,
            Resultado = resultado
        };
    }

    public static ResultadoComandoContexto Fallo(string error)
    {
        return new ResultadoComandoContexto
        {
            Exitoso = false,
            Error = error
        };
    }
}
