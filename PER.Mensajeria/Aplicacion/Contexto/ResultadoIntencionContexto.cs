namespace PER.Mensajeria.Aplicacion.Contexto;

using PER.Mensajeria.Entidad.DTO;

public class ResultadoIntencionContexto
{
    public AccionContextoTipo TipoAccion { get; set; }
    public string? Error { get; set; }
    public string? CodigoComando { get; set; }
    public Dictionary<string, string> ParametrosComando { get; set; } = [];
    public List<DTOMensajeSaliente> MensajesSalientes { get; set; } = [];

    public static ResultadoIntencionContexto Responder(params DTOMensajeSaliente[] mensajesSalientes)
    {
        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Responder,
            MensajesSalientes = mensajesSalientes.ToList()
        };
    }

    public static ResultadoIntencionContexto NoResponder()
    {
        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.NoResponder
        };
    }

    public static ResultadoIntencionContexto PedirComando(string codigoComando, Dictionary<string, string>? parametros = null)
    {
        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Comando,
            CodigoComando = codigoComando,
            ParametrosComando = parametros ?? []
        };
    }

    public static ResultadoIntencionContexto PedirHistorial()
    {
        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Historial
        };
    }

    public static ResultadoIntencionContexto ConError(string error)
    {
        return new ResultadoIntencionContexto
        {
            TipoAccion = AccionContextoTipo.Error,
            Error = error
        };
    }
}
