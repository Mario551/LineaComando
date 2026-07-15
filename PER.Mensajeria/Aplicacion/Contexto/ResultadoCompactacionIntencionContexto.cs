namespace PER.Mensajeria.Aplicacion.Contexto;

public sealed class ResultadoCompactacionIntencionContexto
{
    private ResultadoCompactacionIntencionContexto()
    {
    }

    public bool Exitoso { get; private set; }
    public string? Error { get; private set; }
    public string Contenido { get; private set; } = string.Empty;
    public MetadataRazonamientoIAContexto Metadata { get; private set; } = null!;

    public static ResultadoCompactacionIntencionContexto Exito(
        string contenido,
        MetadataRazonamientoIAContexto metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);
        ValidarMetadata(metadata);

        return new ResultadoCompactacionIntencionContexto
        {
            Exitoso = true,
            Contenido = contenido,
            Metadata = metadata
        };
    }

    public static ResultadoCompactacionIntencionContexto Fallo(
        string error,
        MetadataRazonamientoIAContexto metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ValidarMetadata(metadata);

        return new ResultadoCompactacionIntencionContexto
        {
            Exitoso = false,
            Error = error,
            Metadata = metadata
        };
    }

    private static void ValidarMetadata(MetadataRazonamientoIAContexto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Adaptador);
    }
}
