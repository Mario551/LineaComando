namespace PER.Mensajeria.API.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class DTOIntencionContextoResultado
{
    public DTOAccionContextoTipo TipoAccion { get; set; }
    public string? Error { get; set; }
    public string? CodigoComando { get; set; }
    public Dictionary<string, string> ParametrosComando { get; set; } = [];
    public List<DTOMensajeSaliente> MensajesSalientes { get; set; } = [];

    public static DTOIntencionContextoResultado Responder(params DTOMensajeSaliente[] mensajesSalientes)
    {
        return new DTOIntencionContextoResultado
        {
            TipoAccion = DTOAccionContextoTipo.Responder,
            MensajesSalientes = mensajesSalientes.ToList()
        };
    }

    public static DTOIntencionContextoResultado NoResponder()
    {
        return new DTOIntencionContextoResultado
        {
            TipoAccion = DTOAccionContextoTipo.NoResponder
        };
    }

    public static DTOIntencionContextoResultado PedirComando(string codigoComando, Dictionary<string, string>? parametros = null)
    {
        return new DTOIntencionContextoResultado
        {
            TipoAccion = DTOAccionContextoTipo.Comando,
            CodigoComando = codigoComando,
            ParametrosComando = parametros ?? []
        };
    }

    public static DTOIntencionContextoResultado PedirHistorial()
    {
        return new DTOIntencionContextoResultado
        {
            TipoAccion = DTOAccionContextoTipo.Historial
        };
    }

    public static DTOIntencionContextoResultado ConError(string error)
    {
        return new DTOIntencionContextoResultado
        {
            TipoAccion = DTOAccionContextoTipo.Error,
            Error = error
        };
    }
}
