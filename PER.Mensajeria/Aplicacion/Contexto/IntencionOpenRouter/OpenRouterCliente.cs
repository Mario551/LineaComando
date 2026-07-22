using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PER.Mensajeria.Entidad.DTO.IntencionOpenRouter;

namespace PER.Mensajeria.Aplicacion.Contexto.IntencionOpenRouter;

public class OpenRouterCliente : IOpenRouterCliente
{
    private const string RutaChat = "chat/completions";

    private readonly HttpClient httpClient;
    private readonly ILogger<OpenRouterCliente> logger;

    public OpenRouterCliente(
        HttpClient httpClient,
        ILogger<OpenRouterCliente> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<ResultadoOpenRouterCliente> CompletarChatAsync(
        DTOOpenRouterSolicitudChat solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        string solicitudJson = JsonSerializer.Serialize(solicitud, OpenRouterSerializacion.Opciones);
        using HttpRequestMessage peticion = new(HttpMethod.Post, RutaChat)
        {
            Content = new StringContent(solicitudJson, Encoding.UTF8, "application/json")
        };
        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using HttpResponseMessage respuestaHttp = await httpClient.SendAsync(peticion, cancellationToken);
            string respuestaJson = await respuestaHttp.Content.ReadAsStringAsync(cancellationToken);
            DTOOpenRouterRespuestaChat? respuesta;
            try
            {
                respuesta = DeserializarRespuesta(respuestaJson);
            }
            catch (JsonException excepcion)
            {
                logger.LogError(
                    excepcion,
                    "OpenRouter devolvio JSON invalido. StatusCode={StatusCode}",
                    (int)respuestaHttp.StatusCode);
                return ResultadoOpenRouterCliente.Fallo(
                    respuestaHttp.StatusCode,
                    solicitudJson,
                    respuestaJson,
                    "OpenRouter devolvio JSON invalido.",
                    "invalid_json");
            }

            DTOOpenRouterError? errorOpenRouter = ObtenerError(respuesta);

            if (respuestaHttp.IsSuccessStatusCode
                && respuesta is not null
                && errorOpenRouter is null)
            {
                logger.LogInformation(
                    "OpenRouter respondio correctamente. StatusCode={StatusCode}, Modelo={Modelo}, Proveedor={Proveedor}",
                    (int)respuestaHttp.StatusCode,
                    respuesta.Modelo,
                    respuesta.Proveedor);

                return ResultadoOpenRouterCliente.Exito(
                    respuestaHttp.StatusCode,
                    respuesta,
                    solicitudJson,
                    respuestaJson);
            }

            string? tipoError = ObtenerTipoError(errorOpenRouter);
            string mensaje = errorOpenRouter?.Mensaje
                ?? (respuestaHttp.IsSuccessStatusCode
                    ? "OpenRouter devolvio una respuesta vacia o con error."
                    : $"OpenRouter devolvio HTTP {(int)respuestaHttp.StatusCode}.");

            logger.LogError(
                "OpenRouter devolvio error. StatusCode={StatusCode}, TipoError={TipoError}, Mensaje={Mensaje}",
                (int)respuestaHttp.StatusCode,
                tipoError,
                mensaje);

            return ResultadoOpenRouterCliente.Fallo(
                respuestaHttp.StatusCode,
                solicitudJson,
                respuestaJson,
                mensaje,
                tipoError,
                errorOpenRouter);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException excepcion)
        {
            logger.LogError(excepcion, "La solicitud a OpenRouter supero el timeout configurado.");
            return ResultadoOpenRouterCliente.Fallo(
                null,
                solicitudJson,
                string.Empty,
                "La solicitud a OpenRouter supero el timeout configurado.",
                "timeout");
        }
        catch (HttpRequestException excepcion)
        {
            logger.LogError(excepcion, "Fallo la comunicacion HTTP con OpenRouter.");
            return ResultadoOpenRouterCliente.Fallo(
                excepcion.StatusCode,
                solicitudJson,
                string.Empty,
                excepcion.Message,
                "http_request_error");
        }
    }

    private static DTOOpenRouterRespuestaChat? DeserializarRespuesta(string respuestaJson)
    {
        if (string.IsNullOrWhiteSpace(respuestaJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DTOOpenRouterRespuestaChat>(
            respuestaJson,
            OpenRouterSerializacion.Opciones);
    }

    private static string? ObtenerTipoError(DTOOpenRouterError? error)
    {
        if (error?.Metadata is not JsonElement metadata
            || metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("error_type", out JsonElement tipoError)
            || tipoError.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return tipoError.GetString();
    }

    private static DTOOpenRouterError? ObtenerError(DTOOpenRouterRespuestaChat? respuesta)
    {
        return respuesta?.Error
            ?? respuesta?.Elecciones
                .Select(eleccion => eleccion.Error)
                .FirstOrDefault(error => error is not null);
    }
}
