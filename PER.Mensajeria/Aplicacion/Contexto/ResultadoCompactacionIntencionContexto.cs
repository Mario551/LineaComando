namespace PER.Mensajeria.Aplicacion.Contexto;

public sealed class ResultadoCompactacionIntencionContexto
{
    private ResultadoCompactacionIntencionContexto()
    {
    }

    public bool Exitoso { get; private set; }
    public string? Error { get; private set; }
    public string Contenido { get; private set; } = string.Empty;
    public IReadOnlyList<InformacionTecnicaLlamadaIAContexto> InformacionesTecnicasLlamadasIA { get; private set; } = [];
    public InformacionTecnicaLlamadaIAContexto InformacionTecnicaLlamadaIA => InformacionesTecnicasLlamadasIA[^1];

    public static ResultadoCompactacionIntencionContexto Exito(
        string contenido,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA)
    {
        return Exito(contenido, [informacionTecnicaLlamadaIA]);
    }

    public static ResultadoCompactacionIntencionContexto Exito(
        string contenido,
        IReadOnlyList<InformacionTecnicaLlamadaIAContexto> informacionesTecnicasLlamadasIA)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);
        ValidarInformacionesTecnicasLlamadasIA(informacionesTecnicasLlamadasIA);

        return new ResultadoCompactacionIntencionContexto
        {
            Exitoso = true,
            Contenido = contenido,
            InformacionesTecnicasLlamadasIA = informacionesTecnicasLlamadasIA.ToList()
        };
    }

    public static ResultadoCompactacionIntencionContexto Fallo(
        string error,
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA)
    {
        return Fallo(error, [informacionTecnicaLlamadaIA]);
    }

    public static ResultadoCompactacionIntencionContexto Fallo(
        string error,
        IReadOnlyList<InformacionTecnicaLlamadaIAContexto> informacionesTecnicasLlamadasIA)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ValidarInformacionesTecnicasLlamadasIA(informacionesTecnicasLlamadasIA);

        return new ResultadoCompactacionIntencionContexto
        {
            Exitoso = false,
            Error = error,
            InformacionesTecnicasLlamadasIA = informacionesTecnicasLlamadasIA.ToList()
        };
    }

    private static void ValidarInformacionesTecnicasLlamadasIA(IReadOnlyList<InformacionTecnicaLlamadaIAContexto> informacionesTecnicasLlamadasIA)
    {
        ArgumentNullException.ThrowIfNull(informacionesTecnicasLlamadasIA);
        if (informacionesTecnicasLlamadasIA.Count == 0)
        {
            throw new ArgumentException("La compactacion debe contener informacion tecnica de al menos una llamada IA.", nameof(informacionesTecnicasLlamadasIA));
        }

        foreach (InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA in informacionesTecnicasLlamadasIA)
        {
            ValidarInformacionTecnicaLlamadaIA(informacionTecnicaLlamadaIA);
        }
    }

    private static void ValidarInformacionTecnicaLlamadaIA(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA)
    {
        ArgumentNullException.ThrowIfNull(informacionTecnicaLlamadaIA);
        ArgumentException.ThrowIfNullOrWhiteSpace(informacionTecnicaLlamadaIA.Proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(informacionTecnicaLlamadaIA.Modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(informacionTecnicaLlamadaIA.Adaptador);
    }
}
