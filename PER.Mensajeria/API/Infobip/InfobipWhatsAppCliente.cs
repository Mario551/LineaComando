using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PER.Mensajeria.Entidad.Infobip.DTO;

namespace PER.Mensajeria.API.Infobip;

public class InfobipWhatsAppCliente : IInfobipWhatsAppCliente
{
    private readonly HttpClient httpClient;

    public InfobipWhatsAppCliente(
        HttpClient httpClient,
        ConfiguracionClienteInfobip configuracion)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuracion);
        configuracion.Validar();

        this.httpClient = httpClient;
        this.httpClient.BaseAddress = configuracion.Servidor;
        this.httpClient.Timeout = configuracion.Timeout;
        this.httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("App", configuracion.ApiKey);
        this.httpClient.DefaultRequestHeaders.Accept.Clear();
        this.httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DTOResultadoEnvioInfobipCliente> EnviarAsync(
        DTOInfobipSolicitudEnvio solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitud.CuerpoJson);

        using HttpRequestMessage peticion = new(
            HttpMethod.Post,
            solicitud.Endpoint)
        {
            Content = new StringContent(
                solicitud.CuerpoJson,
                Encoding.UTF8,
                "application/json")
        };

        try
        {
            using HttpResponseMessage respuesta = await httpClient.SendAsync(
                peticion,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            string cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            DTOResultadoEnvioInfobipCliente resultado = new()
            {
                EsExitosoHttp = respuesta.IsSuccessStatusCode,
                StatusHttp = (int)respuesta.StatusCode,
                CuerpoRespuesta = cuerpo
            };

            if (respuesta.IsSuccessStatusCode)
            {
                resultado.Respuesta = DeserializarRespuesta(cuerpo, resultado);
            }
            else
            {
                resultado.ErrorRespuesta = DeserializarError(cuerpo);
                resultado.ErrorTecnico = CrearErrorHttp(resultado.StatusHttp, cuerpo);
            }

            return resultado;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DTOResultadoEnvioInfobipCliente
            {
                EsTimeout = true,
                EsResultadoIncierto = true,
                ErrorTecnico = "La solicitud a Infobip excedió el tiempo máximo configurado."
            };
        }
        catch (HttpRequestException excepcion)
        {
            return new DTOResultadoEnvioInfobipCliente
            {
                EsResultadoIncierto = true,
                ErrorTecnico = $"No fue posible completar la solicitud HTTP a Infobip: {excepcion.Message}"
            };
        }
    }

    private static DTOInfobipRespuestaEnvio? DeserializarRespuesta(
        string cuerpo,
        DTOResultadoEnvioInfobipCliente resultado)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            resultado.EsResultadoIncierto = true;
            resultado.ErrorTecnico = "Infobip devolvió una respuesta exitosa sin contenido.";
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DTOInfobipRespuestaEnvio>(
                cuerpo,
                InfobipSerializacion.Opciones);
        }
        catch (JsonException excepcion)
        {
            resultado.EsResultadoIncierto = true;
            resultado.ErrorTecnico =
                $"Infobip devolvió JSON inválido en una respuesta exitosa: {excepcion.Message}";
            return null;
        }
    }

    private static DTOInfobipError? DeserializarError(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DTOInfobipError>(
                cuerpo,
                InfobipSerializacion.Opciones);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string CrearErrorHttp(int? statusHttp, string cuerpo)
    {
        string detalle = string.IsNullOrWhiteSpace(cuerpo)
            ? "sin cuerpo de respuesta"
            : cuerpo;
        return $"Infobip devolvió HTTP {statusHttp}: {detalle}";
    }
}
