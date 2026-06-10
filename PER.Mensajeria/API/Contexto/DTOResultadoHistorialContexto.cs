namespace PER.Mensajeria.API.Contexto;

public class DTOResultadoHistorialContexto
{
    public bool Exitoso { get; set; }
    public string? Historial { get; set; }
    public string? Error { get; set; }

    public static DTOResultadoHistorialContexto Exito(string historial)
    {
        return new DTOResultadoHistorialContexto
        {
            Exitoso = true,
            Historial = historial
        };
    }

    public static DTOResultadoHistorialContexto Fallo(string error)
    {
        return new DTOResultadoHistorialContexto
        {
            Exitoso = false,
            Error = error
        };
    }
}
