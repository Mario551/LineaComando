namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class ResultadoCompactacionOpenCode
{
    private ResultadoCompactacionOpenCode()
    {
    }

    public bool Exitoso { get; private set; }
    public bool LimiteVentana { get; private set; }
    public string? Contenido { get; private set; }
    public string? Error { get; private set; }
    public InformacionTecnicaLlamadaIAContexto InformacionTecnicaLlamadaIA { get; private set; } = null!;

    public static ResultadoCompactacionOpenCode Exito(
        string contenido,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);
        ArgumentNullException.ThrowIfNull(informacionTecnicaLlamadaIA);

        return new ResultadoCompactacionOpenCode
        {
            Exitoso = true,
            Contenido = contenido,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA
        };
    }

    public static ResultadoCompactacionOpenCode Fallo(
        string error,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        bool limiteVentana = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentNullException.ThrowIfNull(informacionTecnicaLlamadaIA);

        return new ResultadoCompactacionOpenCode
        {
            Exitoso = false,
            LimiteVentana = limiteVentana,
            Error = error,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA
        };
    }
}
