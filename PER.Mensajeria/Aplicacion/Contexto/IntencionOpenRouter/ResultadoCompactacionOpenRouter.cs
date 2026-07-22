namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public sealed class ResultadoCompactacionOpenRouter
{
    private ResultadoCompactacionOpenRouter()
    {
    }

    public bool Exitoso { get; private set; }
    public bool LimiteVentana { get; private set; }
    public string? Contenido { get; private set; }
    public string? Error { get; private set; }
    public InformacionTecnicaLlamadaIAContexto InformacionTecnicaLlamadaIA { get; private set; } = null!;

    public static ResultadoCompactacionOpenRouter Exito(
        string contenido,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);
        ArgumentNullException.ThrowIfNull(informacionTecnicaLlamadaIA);

        return new ResultadoCompactacionOpenRouter
        {
            Exitoso = true,
            Contenido = contenido,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA
        };
    }

    public static ResultadoCompactacionOpenRouter Fallo(
        string error,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        bool limiteVentana = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentNullException.ThrowIfNull(informacionTecnicaLlamadaIA);

        return new ResultadoCompactacionOpenRouter
        {
            Exitoso = false,
            LimiteVentana = limiteVentana,
            Error = error,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA
        };
    }
}
