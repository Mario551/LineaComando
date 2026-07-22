using System.Net;
using PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public sealed class ResultadoOpenRouterCliente
{
    private ResultadoOpenRouterCliente()
    {
    }

    public bool Exitoso { get; private set; }
    public HttpStatusCode? CodigoEstado { get; private set; }
    public DTOOpenRouterRespuestaChat? Respuesta { get; private set; }
    public DTOOpenRouterError? ErrorOpenRouter { get; private set; }
    public string SolicitudJson { get; private set; } = string.Empty;
    public string RespuestaJson { get; private set; } = string.Empty;
    public string? TipoError { get; private set; }
    public string? Error { get; private set; }

    public static ResultadoOpenRouterCliente Exito(
        HttpStatusCode codigoEstado,
        DTOOpenRouterRespuestaChat respuesta,
        string solicitudJson,
        string respuestaJson)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        return new ResultadoOpenRouterCliente
        {
            Exitoso = true,
            CodigoEstado = codigoEstado,
            Respuesta = respuesta,
            SolicitudJson = solicitudJson,
            RespuestaJson = respuestaJson
        };
    }

    public static ResultadoOpenRouterCliente Fallo(
        HttpStatusCode? codigoEstado,
        string solicitudJson,
        string respuestaJson,
        string error,
        string? tipoError = null,
        DTOOpenRouterError? errorOpenRouter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ResultadoOpenRouterCliente
        {
            Exitoso = false,
            CodigoEstado = codigoEstado,
            SolicitudJson = solicitudJson,
            RespuestaJson = respuestaJson,
            TipoError = tipoError,
            Error = error,
            ErrorOpenRouter = errorOpenRouter
        };
    }
}
