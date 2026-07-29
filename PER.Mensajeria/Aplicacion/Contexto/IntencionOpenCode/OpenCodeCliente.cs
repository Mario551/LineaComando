using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Entidad.DTO.IntencionOpenCode;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenCode;

public sealed class OpenCodeCliente : IOpenCodeCliente
{
    private const string RutaSesiones = "session";

    private readonly HttpClient httpClient;
    private readonly ILogger<OpenCodeCliente> logger;

    public OpenCodeCliente(
        HttpClient httpClient,
        ILogger<OpenCodeCliente> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public Task<ResultadoOpenCodeCliente<DTOOpenCodeSesion>> CrearSesionAsync(
        DTOOpenCodeCrearSesionSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        return EnviarAsync<DTOOpenCodeCrearSesionSolicitud, DTOOpenCodeSesion>(
            HttpMethod.Post,
            RutaSesiones,
            solicitud,
            false,
            cancellationToken);
    }

    public Task<ResultadoOpenCodeCliente<DTOOpenCodeRespuestaMensaje>> EnviarMensajeAsync(
        string idSesion,
        DTOOpenCodeMensajeSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idSesion);
        ArgumentNullException.ThrowIfNull(solicitud);

        string ruta = $"{RutaSesiones}/{Uri.EscapeDataString(idSesion)}/message";
        return EnviarAsync<DTOOpenCodeMensajeSolicitud, DTOOpenCodeRespuestaMensaje>(
            HttpMethod.Post,
            ruta,
            solicitud,
            false,
            cancellationToken);
    }

    public Task<ResultadoOpenCodeCliente<bool>> AbortarSesionAsync(
        string idSesion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idSesion);

        string ruta = $"{RutaSesiones}/{Uri.EscapeDataString(idSesion)}/abort";
        return EnviarSinCuerpoAsync(
            HttpMethod.Post,
            ruta,
            true,
            cancellationToken);
    }

    public Task<ResultadoOpenCodeCliente<bool>> EliminarSesionAsync(
        string idSesion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idSesion);

        string ruta = $"{RutaSesiones}/{Uri.EscapeDataString(idSesion)}";
        return EnviarSinCuerpoAsync(
            HttpMethod.Delete,
            ruta,
            true,
            cancellationToken);
    }

    private async Task<ResultadoOpenCodeCliente<TRespuesta>> EnviarAsync<TSolicitud, TRespuesta>(
        HttpMethod metodo,
        string ruta,
        TSolicitud solicitud,
        bool falloEsAdvertencia,
        CancellationToken cancellationToken)
    {
        string solicitudJson = JsonSerializer.Serialize(
            solicitud,
            OpenCodeSerializacion.Opciones);
        using HttpRequestMessage peticion = CrearPeticion(
            metodo,
            ruta,
            solicitudJson);

        try
        {
            using HttpResponseMessage respuestaHttp =
                await httpClient.SendAsync(peticion, cancellationToken);
            string respuestaJson =
                await respuestaHttp.Content.ReadAsStringAsync(cancellationToken);

            if (!respuestaHttp.IsSuccessStatusCode)
            {
                return CrearFallo<TRespuesta>(
                    respuestaHttp.StatusCode,
                    solicitudJson,
                    respuestaJson,
                    falloEsAdvertencia);
            }

            TRespuesta? respuesta;
            try
            {
                respuesta = JsonSerializer.Deserialize<TRespuesta>(
                    respuestaJson,
                    OpenCodeSerializacion.Opciones);
            }
            catch (JsonException excepcion)
            {
                RegistrarFallo(
                    excepcion,
                    falloEsAdvertencia,
                    "OpenCode devolvio JSON invalido. StatusCode={StatusCode}",
                    (int)respuestaHttp.StatusCode);
                return ResultadoOpenCodeCliente<TRespuesta>.Fallo(
                    respuestaHttp.StatusCode,
                    solicitudJson,
                    respuestaJson,
                    "OpenCode devolvio JSON invalido.",
                    "invalid_json");
            }

            if (respuesta is null)
            {
                RegistrarFallo(
                    null,
                    falloEsAdvertencia,
                    "OpenCode devolvio una respuesta vacia. StatusCode={StatusCode}",
                    (int)respuestaHttp.StatusCode);
                return ResultadoOpenCodeCliente<TRespuesta>.Fallo(
                    respuestaHttp.StatusCode,
                    solicitudJson,
                    respuestaJson,
                    "OpenCode devolvio una respuesta vacia.",
                    "empty_response");
            }

            return ResultadoOpenCodeCliente<TRespuesta>.Exito(
                respuestaHttp.StatusCode,
                respuesta,
                solicitudJson,
                respuestaJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException excepcion)
        {
            RegistrarFallo(
                excepcion,
                falloEsAdvertencia,
                "La solicitud a OpenCode supero el timeout configurado.");
            return ResultadoOpenCodeCliente<TRespuesta>.Fallo(
                null,
                solicitudJson,
                string.Empty,
                "La solicitud a OpenCode supero el timeout configurado.",
                "timeout");
        }
        catch (HttpRequestException excepcion)
        {
            RegistrarFallo(
                excepcion,
                falloEsAdvertencia,
                "Fallo la comunicacion HTTP con OpenCode.");
            return ResultadoOpenCodeCliente<TRespuesta>.Fallo(
                excepcion.StatusCode,
                solicitudJson,
                string.Empty,
                excepcion.Message,
                "http_request_error");
        }
    }

    private async Task<ResultadoOpenCodeCliente<bool>> EnviarSinCuerpoAsync(
        HttpMethod metodo,
        string ruta,
        bool falloEsAdvertencia,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage peticion = CrearPeticion(
            metodo,
            ruta,
            null);

        try
        {
            using HttpResponseMessage respuestaHttp =
                await httpClient.SendAsync(peticion, cancellationToken);
            string respuestaJson =
                await respuestaHttp.Content.ReadAsStringAsync(cancellationToken);

            if (!respuestaHttp.IsSuccessStatusCode)
            {
                return CrearFallo<bool>(
                    respuestaHttp.StatusCode,
                    string.Empty,
                    respuestaJson,
                    falloEsAdvertencia);
            }

            bool respuesta = true;
            if (!string.IsNullOrWhiteSpace(respuestaJson)
                && bool.TryParse(respuestaJson, out bool respuestaDeserializada))
            {
                respuesta = respuestaDeserializada;
            }

            return ResultadoOpenCodeCliente<bool>.Exito(
                respuestaHttp.StatusCode,
                respuesta,
                string.Empty,
                respuestaJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException excepcion)
        {
            RegistrarFallo(
                excepcion,
                falloEsAdvertencia,
                "La limpieza de sesion OpenCode supero el timeout.");
            return ResultadoOpenCodeCliente<bool>.Fallo(
                null,
                string.Empty,
                string.Empty,
                "La limpieza de sesion OpenCode supero el timeout.",
                "timeout");
        }
        catch (HttpRequestException excepcion)
        {
            RegistrarFallo(
                excepcion,
                falloEsAdvertencia,
                "Fallo la limpieza HTTP de la sesion OpenCode.");
            return ResultadoOpenCodeCliente<bool>.Fallo(
                excepcion.StatusCode,
                string.Empty,
                string.Empty,
                excepcion.Message,
                "http_request_error");
        }
    }

    private ResultadoOpenCodeCliente<TRespuesta> CrearFallo<TRespuesta>(
        HttpStatusCode codigoEstado,
        string solicitudJson,
        string respuestaJson,
        bool falloEsAdvertencia)
    {
        DTOOpenCodeError? errorOpenCode = DeserializarError(respuestaJson);
        string? tipoError = ObtenerTipoError(errorOpenCode);
        string mensaje = ObtenerMensajeError(errorOpenCode)
            ?? $"OpenCode devolvio HTTP {(int)codigoEstado}.";

        RegistrarFallo(
            null,
            falloEsAdvertencia,
            "OpenCode devolvio error. StatusCode={StatusCode}, TipoError={TipoError}, Mensaje={Mensaje}",
            (int)codigoEstado,
            tipoError,
            mensaje);

        return ResultadoOpenCodeCliente<TRespuesta>.Fallo(
            codigoEstado,
            solicitudJson,
            respuestaJson,
            mensaje,
            tipoError,
            errorOpenCode);
    }

    private static HttpRequestMessage CrearPeticion(
        HttpMethod metodo,
        string ruta,
        string? solicitudJson)
    {
        HttpRequestMessage peticion = new(metodo, ruta);
        peticion.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        if (solicitudJson is not null)
        {
            peticion.Content = new StringContent(
                solicitudJson,
                Encoding.UTF8,
                "application/json");
        }

        return peticion;
    }

    private static DTOOpenCodeError? DeserializarError(string respuestaJson)
    {
        if (string.IsNullOrWhiteSpace(respuestaJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DTOOpenCodeError>(
                respuestaJson,
                OpenCodeSerializacion.Opciones);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ObtenerTipoError(DTOOpenCodeError? error)
    {
        if (!string.IsNullOrWhiteSpace(error?.Nombre))
        {
            return error.Nombre;
        }

        return null;
    }

    private static string? ObtenerMensajeError(DTOOpenCodeError? error)
    {
        if (error is null
            || error.Datos.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (string propiedad in new[] { "message", "error", "reason" })
        {
            if (error.Datos.TryGetProperty(propiedad, out JsonElement valor)
                && valor.ValueKind == JsonValueKind.String)
            {
                return valor.GetString();
            }
        }

        return null;
    }

    private void RegistrarFallo(
        Exception? excepcion,
        bool falloEsAdvertencia,
        string mensaje,
        params object?[] argumentos)
    {
        if (falloEsAdvertencia)
        {
            logger.LogWarning(excepcion, mensaje, argumentos);
            return;
        }

        logger.LogError(excepcion, mensaje, argumentos);
    }
}
