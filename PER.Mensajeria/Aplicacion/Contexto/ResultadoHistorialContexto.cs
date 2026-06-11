namespace PER.Mensajeria.Aplicacion.Contexto;

public class ResultadoHistorialContexto
{
    public bool Exitoso { get; set; }
    public string? Historial { get; set; }
    public string? Error { get; set; }

    public static ResultadoHistorialContexto Exito(string historial)
    {
        return new ResultadoHistorialContexto
        {
            Exitoso = true,
            Historial = historial
        };
    }

    public static ResultadoHistorialContexto Fallo(string error)
    {
        return new ResultadoHistorialContexto
        {
            Exitoso = false,
            Error = error
        };
    }
}
