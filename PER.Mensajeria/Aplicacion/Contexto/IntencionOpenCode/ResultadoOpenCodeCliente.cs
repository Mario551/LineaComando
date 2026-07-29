using System.Net;
using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class ResultadoOpenCodeCliente<TRespuesta>
{
    private ResultadoOpenCodeCliente()
    {
    }

    public bool Exitoso { get; private set; }
    public HttpStatusCode? CodigoEstado { get; private set; }
    public TRespuesta? Respuesta { get; private set; }
    public DTOOpenCodeError? ErrorOpenCode { get; private set; }
    public string SolicitudJson { get; private set; } = string.Empty;
    public string RespuestaJson { get; private set; } = string.Empty;
    public string? TipoError { get; private set; }
    public string? Error { get; private set; }

    public static ResultadoOpenCodeCliente<TRespuesta> Exito(
        HttpStatusCode codigoEstado,
        TRespuesta respuesta,
        string solicitudJson,
        string respuestaJson)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        return new ResultadoOpenCodeCliente<TRespuesta>
        {
            Exitoso = true,
            CodigoEstado = codigoEstado,
            Respuesta = respuesta,
            SolicitudJson = solicitudJson,
            RespuestaJson = respuestaJson
        };
    }

    public static ResultadoOpenCodeCliente<TRespuesta> Fallo(
        HttpStatusCode? codigoEstado,
        string solicitudJson,
        string respuestaJson,
        string error,
        string? tipoError = null,
        DTOOpenCodeError? errorOpenCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new ResultadoOpenCodeCliente<TRespuesta>
        {
            Exitoso = false,
            CodigoEstado = codigoEstado,
            SolicitudJson = solicitudJson,
            RespuestaJson = respuestaJson,
            TipoError = tipoError,
            Error = error,
            ErrorOpenCode = errorOpenCode
        };
    }
}
