namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public sealed class ResultadoIntencionContexto
{
    private ResultadoIntencionContexto()
    {
    }

    public AccionContextoTipo TipoAccion { get; private set; }
    public MetadataRazonamientoIAContexto Metadata { get; private set; } = null!;
    public string ContenidoDecision { get; private set; } = string.Empty;
    public string? Error { get; private set; }
    public string? CodigoComando { get; private set; }
    public Dictionary<string, string> ParametrosComando { get; private set; } = [];
    public List<DTOMensajeSaliente> MensajesSalientes { get; private set; } = [];
    public DeteccionLimiteVentanaContextoTipo? DeteccionLimiteVentana { get; private set; }

    public static ResultadoIntencionContexto Responder(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision,
        params DTOMensajeSaliente[] mensajesSalientes)
    {
        ValidarContrato(metadata, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Responder,
            Metadata = metadata,
            ContenidoDecision = contenidoDecision,
            MensajesSalientes = mensajesSalientes.ToList()
        };
    }

    public static ResultadoIntencionContexto NoResponder(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision)
    {
        ValidarContrato(metadata, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.NoResponder,
            Metadata = metadata,
            ContenidoDecision = contenidoDecision
        };
    }

    public static ResultadoIntencionContexto PedirComando(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision,
        string codigoComando,
        Dictionary<string, string>? parametros = null)
    {
        ValidarContrato(metadata, contenidoDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoComando);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Comando,
            Metadata = metadata,
            ContenidoDecision = contenidoDecision,
            CodigoComando = codigoComando,
            ParametrosComando = parametros ?? []
        };
    }

    public static ResultadoIntencionContexto PedirHistorial(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision)
    {
        ValidarContrato(metadata, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Historial,
            Metadata = metadata,
            ContenidoDecision = contenidoDecision
        };
    }

    public static ResultadoIntencionContexto ConError(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision,
        string error)
    {
        ValidarContrato(metadata, contenidoDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Error,
            Metadata = metadata,
            ContenidoDecision = contenidoDecision,
            Error = error
        };
    }

    public static ResultadoIntencionContexto LimiteVentanaAlcanzado(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision,
        DeteccionLimiteVentanaContextoTipo deteccion)
    {
        ValidarContrato(metadata, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.LimiteVentanaAlcanzado,
            Metadata = metadata,
            ContenidoDecision = contenidoDecision,
            DeteccionLimiteVentana = deteccion
        };
    }

    private static void ValidarContrato(
        MetadataRazonamientoIAContexto metadata,
        string contenidoDecision)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(contenidoDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Adaptador);
    }
}
