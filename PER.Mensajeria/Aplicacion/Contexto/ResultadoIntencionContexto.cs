namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public sealed class ResultadoIntencionContexto
{
    private ResultadoIntencionContexto()
    {
    }

    public AccionContextoTipo TipoAccion { get; private set; }
    public InformacionTecnicaLlamadaIAContexto InformacionTecnicaLlamadaIA { get; private set; } = null!;
    public string ContenidoDecision { get; private set; } = string.Empty;
    public string? Error { get; private set; }
    public string? CodigoComando { get; private set; }
    public string? ToolCallID { get; private set; }
    public int? CiclosHaciaAtras { get; private set; }
    public Dictionary<string, string> ParametrosComando { get; private set; } = [];
    public List<DTOMensajeSaliente> MensajesSalientes { get; private set; } = [];
    public DeteccionLimiteVentanaContextoTipo? DeteccionLimiteVentana { get; private set; }

    public static ResultadoIntencionContexto Responder(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision,
        params DTOMensajeSaliente[] mensajesSalientes)
    {
        ValidarContrato(informacionTecnicaLlamadaIA, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Responder,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA,
            ContenidoDecision = contenidoDecision,
            MensajesSalientes = mensajesSalientes.ToList()
        };
    }

    public static ResultadoIntencionContexto NoResponder(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision)
    {
        ValidarContrato(informacionTecnicaLlamadaIA, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.NoResponder,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA,
            ContenidoDecision = contenidoDecision
        };
    }

    public static ResultadoIntencionContexto PedirComando(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision,
        string codigoComando,
        Dictionary<string, string>? parametros = null,
        string? toolCallID = null)
    {
        ValidarContrato(informacionTecnicaLlamadaIA, contenidoDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoComando);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Comando,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA,
            ContenidoDecision = contenidoDecision,
            CodigoComando = codigoComando,
            ToolCallID = toolCallID,
            ParametrosComando = parametros ?? []
        };
    }

    public static ResultadoIntencionContexto ConsultarMensajesLineaAnterior(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision,
        int ciclosHaciaAtras,
        string? toolCallID = null)
    {
        ValidarContrato(informacionTecnicaLlamadaIA, contenidoDecision);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ciclosHaciaAtras);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.ConsultarMensajesLineaAnterior,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA,
            ContenidoDecision = contenidoDecision,
            CiclosHaciaAtras = ciclosHaciaAtras,
            ToolCallID = toolCallID
        };
    }

    public static ResultadoIntencionContexto ConError(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision,
        string error)
    {
        ValidarContrato(informacionTecnicaLlamadaIA, contenidoDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Error,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA,
            ContenidoDecision = contenidoDecision,
            Error = error
        };
    }

    public static ResultadoIntencionContexto LimiteVentanaAlcanzado(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision,
        DeteccionLimiteVentanaContextoTipo deteccion)
    {
        ValidarContrato(informacionTecnicaLlamadaIA, contenidoDecision);

        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.LimiteVentanaAlcanzado,
            InformacionTecnicaLlamadaIA = informacionTecnicaLlamadaIA,
            ContenidoDecision = contenidoDecision,
            DeteccionLimiteVentana = deteccion
        };
    }

    private static void ValidarContrato(
        InformacionTecnicaLlamadaIAContexto informacionTecnicaLlamadaIA,
        string contenidoDecision)
    {
        ArgumentNullException.ThrowIfNull(informacionTecnicaLlamadaIA);
        ArgumentException.ThrowIfNullOrWhiteSpace(contenidoDecision);
        ArgumentException.ThrowIfNullOrWhiteSpace(informacionTecnicaLlamadaIA.Proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(informacionTecnicaLlamadaIA.Modelo);
        ArgumentException.ThrowIfNullOrWhiteSpace(informacionTecnicaLlamadaIA.Adaptador);
    }
}
